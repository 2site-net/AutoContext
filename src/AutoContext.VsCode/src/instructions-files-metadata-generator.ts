// Builds the instructions files metadata manifest from each
// `instructions/*.instructions.md` source file and writes it to
// `resources/instructions-files.metadata.json`. Companion of the
// hand-authored `resources/instructions-files.json`.
//
// Self-executable: tsx src/instructions-files-metadata-generator.ts

import { createHash } from 'node:crypto';
import { existsSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { InstructionsFileParser } from './instructions-file-parser.js';
import { InstructionsFileSectionsParser } from './instructions-file-sections-parser.js';
import { ALWAYS_ATTACHED_INSTRUCTIONS_FILES_SET } from './always-attached-instructions-files.js';
import type { InstructionsFileMetadataEntry } from '#types/instructions-file-metadata-entry.js';
import type { InstructionsFileSection } from '#types/instructions-file-section.js';
import type { InstructionsFilesMetadata } from '#types/instructions-files-metadata.js';

const SCHEMA_VERSION = '1';
const NAME_PATTERN = /^([a-z0-9][a-z0-9-]*) \(v(\d+\.\d+\.\d+)\)$/;
const FRONTMATTER_STRIP_PATTERN = /^---\r?\n[\s\S]*?\r?\n---\r?\n?/;

interface CuratedManifest {
    readonly instructions: ReadonlyArray<{ readonly name: string }>;
}

export function generateInstructionsFilesMetadata(extensionRoot: string): InstructionsFilesMetadata {
    const instructionsDir = join(extensionRoot, 'instructions');
    const fileNames = readdirSync(instructionsDir)
        .filter(n => n.endsWith('.instructions.md'))
        .sort();

    const entries: InstructionsFileMetadataEntry[] = [];
    for (const fileName of fileNames) {
        entries.push(buildEntry(instructionsDir, fileName));
    }

    crossValidate(entries, extensionRoot);

    entries.sort((a, b) => a.key.localeCompare(b.key));

    return { schemaVersion: SCHEMA_VERSION, instructions: entries };
}

function buildEntry(instructionsDir: string, fileName: string): InstructionsFileMetadataEntry {
    const filePath = join(instructionsDir, fileName);
    const content = readFileSync(filePath, 'utf-8');
    const changelogName = fileName.replace(/\.instructions\.md$/, '.CHANGELOG.md');
    const hasChangelog = existsSync(join(instructionsDir, changelogName));
    // Only the frontmatter is relevant here; the parser's per-bullet diagnostics
    // (duplicate-id, malformed-id, missing-id) belong to a different consumer
    // and must not gate metadata generation.
    const frontmatter = InstructionsFileParser.parseFrontmatter(content);

    if (!frontmatter.name) {
        fail(fileName, 'missing required `name` frontmatter field');
    }
    const nameMatch = NAME_PATTERN.exec(frontmatter.name);
    if (!nameMatch) {
        fail(fileName, `\`name\` does not match \`<key> (vX.Y.Z)\`: '${frontmatter.name}'`);
    }
    const key = nameMatch[1];
    const version = nameMatch[2];

    const expectedKey = fileName.replace(/\.instructions\.md$/, '');
    if (key !== expectedKey) {
        fail(fileName, `\`name\` key '${key}' does not equal file basename '${expectedKey}'`);
    }

    const description = frontmatter.description?.trim();
    if (!description) {
        fail(fileName, 'missing or empty `description` frontmatter field');
    }

    const applyTo = frontmatter.applyTo;
    if (applyTo !== undefined && applyTo.trim() === '') {
        fail(fileName, '`applyTo` is present but empty');
    }

    const normalizedBody = stripFrontmatter(content);
    const sections = extractSections(normalizedBody, fileName);
    const contentHash = 'sha256:' + createHash('sha256').update(normalizedBody, 'utf-8').digest('hex');

    return {
        key,
        fileName,
        name: frontmatter.name,
        version,
        description,
        ...(applyTo !== undefined ? { applyTo } : {}),
        hasChangelog,
        contentHash,
        sections,
    };
}

function stripFrontmatter(content: string): string {
    return content.replace(FRONTMATTER_STRIP_PATTERN, '');
}

function extractSections(body: string, fileName: string): readonly InstructionsFileSection[] {
    const parsed = InstructionsFileSectionsParser.parse(body);
    const sections: InstructionsFileSection[] = [];
    const seenAnchors = new Set<string>();
    for (const section of parsed) {
        if (seenAnchors.has(section.anchor)) {
            fail(fileName, `duplicate section anchor '${section.anchor}' (heading collision)`);
        }
        seenAnchors.add(section.anchor);
        sections.push({
            heading: section.heading,
            anchor: section.anchor,
            ...(section.parent !== undefined ? { parent: section.parent } : {}),
        });
    }
    return sections;
}

function crossValidate(entries: readonly InstructionsFileMetadataEntry[], extensionRoot: string): void {
    const curatedPath = join(extensionRoot, 'resources', 'instructions-files.json');
    const curated = JSON.parse(readFileSync(curatedPath, 'utf-8')) as CuratedManifest;
    const curatedNames = new Set(curated.instructions.map(i => i.name));
    // Always-attached files live outside the curated manifest by design (see
    // package-instructions-manifest-generator). They are picked up by the
    // metadata generator (so their bodies and section indices are
    // discoverable via the LM tools) but exempt from the curated cross-check.
    const generatedNames = new Set(
        entries.map(e => e.fileName).filter(n => !ALWAYS_ATTACHED_INSTRUCTIONS_FILES_SET.has(n)),
    );

    const missingInCurated = [...generatedNames].filter(n => !curatedNames.has(n));
    const missingInGenerated = [...curatedNames].filter(n => !generatedNames.has(n));

    if (missingInCurated.length > 0 || missingInGenerated.length > 0) {
        const parts: string[] = [];
        if (missingInCurated.length > 0) {
            parts.push(`present in instructions/ but not in instructions-files.json: ${missingInCurated.join(', ')}`);
        }
        if (missingInGenerated.length > 0) {
            parts.push(`listed in instructions-files.json but missing from instructions/: ${missingInGenerated.join(', ')}`);
        }
        throw new Error(`Instruction file set mismatch:\n  ${parts.join('\n  ')}`);
    }
}

function fail(fileName: string, message: string): never {
    throw new Error(`[${fileName}] ${message}`);
}

if (process.argv[1]?.replace(/\\/g, '/').endsWith('/src/instructions-files-metadata-generator.ts')) {
    const root = join(dirname(fileURLToPath(import.meta.url)), '..');
    const metadata = generateInstructionsFilesMetadata(root);
    const outPath = join(root, 'resources', 'instructions-files.metadata.json');
    writeFileSync(outPath, JSON.stringify(metadata, null, 2) + '\n', 'utf-8');
    console.log(`Generated metadata for ${metadata.instructions.length} instructions file(s).`);
}
