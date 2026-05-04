// Builds the instructions-files metadata manifest from each
// `instructions/*.instructions.md` source file and writes it to
// `resources/instructions-files.metadata.json`. Companion of the
// hand-authored `resources/instructions-files.json`.
//
// Self-executable: tsx src/instructions-files-metadata-generator.ts

import { createHash } from 'node:crypto';
import { readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { InstructionsFileParser } from './instructions-file-parser.js';
import type { InstructionsFileMetadataEntry } from '#types/instructions-file-metadata-entry.js';
import type { InstructionsFileSection } from '#types/instructions-file-section.js';
import type { InstructionsFilesMetadata } from '#types/instructions-files-metadata.js';

const SCHEMA_VERSION = '1';
const NAME_PATTERN = /^([a-z0-9][a-z0-9-]*) \(v(\d+\.\d+\.\d+)\)$/;
const FRONTMATTER_STRIP_PATTERN = /^---\r?\n[\s\S]*?\r?\n---\r?\n?/;
const HEADING_PATTERN = /^(#{2,3}) +(.+?)\s*$/;
const FENCE_PATTERN = /^```/;

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
        entries.push(buildEntry(join(instructionsDir, fileName), fileName));
    }

    crossValidate(entries, extensionRoot);

    entries.sort((a, b) => a.id.localeCompare(b.id));

    return { schemaVersion: SCHEMA_VERSION, instructions: entries };
}

function buildEntry(filePath: string, fileName: string): InstructionsFileMetadataEntry {
    const content = readFileSync(filePath, 'utf-8');
    // Only the frontmatter is relevant here; the parser's per-bullet diagnostics
    // (duplicate-id, malformed-id, missing-id) belong to a different consumer
    // and must not gate metadata generation.
    const frontmatter = InstructionsFileParser.parseFrontmatter(content);

    if (!frontmatter.name) {
        fail(fileName, 'missing required `name` frontmatter field');
    }
    const nameMatch = NAME_PATTERN.exec(frontmatter.name);
    if (!nameMatch) {
        fail(fileName, `\`name\` does not match \`<id> (vX.Y.Z)\`: '${frontmatter.name}'`);
    }
    const id = nameMatch[1];
    const version = nameMatch[2];

    const expectedId = fileName.replace(/\.instructions\.md$/, '');
    if (id !== expectedId) {
        fail(fileName, `\`name\` id '${id}' does not equal file basename '${expectedId}'`);
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
        id,
        fileName,
        name: frontmatter.name,
        version,
        description,
        ...(applyTo !== undefined ? { applyTo } : {}),
        contentHash,
        sections,
    };
}

function stripFrontmatter(content: string): string {
    return content.replace(FRONTMATTER_STRIP_PATTERN, '');
}

function extractSections(body: string, fileName: string): readonly InstructionsFileSection[] {
    interface RawHeading {
        level: 2 | 3;
        heading: string;
        charStart: number;
        parent?: string;
    }

    const lines = body.split('\n');
    const raw: RawHeading[] = [];
    let offset = 0;
    let inFence = false;
    let lastH2: string | undefined;

    for (const line of lines) {
        if (FENCE_PATTERN.test(line)) {
            inFence = !inFence;
            offset += line.length + 1;
            continue;
        }
        if (!inFence) {
            const match = HEADING_PATTERN.exec(line);
            if (match) {
                const level = match[1].length as 2 | 3;
                const heading = match[2].trim();
                if (level === 2) {
                    raw.push({ level, heading, charStart: offset });
                    lastH2 = heading;
                } else {
                    raw.push({ level, heading, charStart: offset, parent: lastH2 });
                }
            }
        }
        offset += line.length + 1;
    }

    const sections: InstructionsFileSection[] = [];
    const seenAnchors = new Set<string>();
    for (let i = 0; i < raw.length; i++) {
        const r = raw[i];
        const charEnd = computeCharEnd(raw, i, body.length);
        const baseSlug = slugify(r.heading);
        const anchor = r.parent ? `${slugify(r.parent)}-${baseSlug}` : baseSlug;

        if (seenAnchors.has(anchor)) {
            fail(fileName, `duplicate section anchor '${anchor}' (heading collision)`);
        }
        seenAnchors.add(anchor);

        sections.push({
            heading: r.heading,
            level: r.level,
            anchor,
            ...(r.parent !== undefined ? { parent: r.parent } : {}),
            charStart: r.charStart,
            charEnd,
        });
    }
    return sections;
}

function computeCharEnd(
    raw: ReadonlyArray<{ readonly level: 2 | 3; readonly charStart: number }>,
    index: number,
    bodyLength: number,
): number {
    const current = raw[index];
    for (let j = index + 1; j < raw.length; j++) {
        if (raw[j].level <= current.level) {
            return raw[j].charStart;
        }
    }
    return bodyLength;
}

function slugify(heading: string): string {
    return heading
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, '-')
        .replace(/^-+|-+$/g, '');
}

function crossValidate(entries: readonly InstructionsFileMetadataEntry[], extensionRoot: string): void {
    const curatedPath = join(extensionRoot, 'resources', 'instructions-files.json');
    const curated = JSON.parse(readFileSync(curatedPath, 'utf-8')) as CuratedManifest;
    const curatedNames = new Set(curated.instructions.map(i => i.name));
    // copilot.instructions.md is the always-attached file; it lives outside
    // the curated manifest by design (see package-instructions-manifest-generator).
    const generatedNames = new Set(
        entries.map(e => e.fileName).filter(n => n !== 'copilot.instructions.md'),
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
