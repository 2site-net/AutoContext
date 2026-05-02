import { readFileSync, existsSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { InstructionsFileParser } from './instructions-file-parser.js';
import type { InstructionsFileMetadata } from '#types/instructions-file-metadata.js';

export class InstructionsFileMetadataReader {
    constructor(private readonly extensionPath: string) {}

    /**
     * Reads frontmatter and changelog presence for every
     * `*.instructions.md` file shipped under `instructions/`. Returns
     * a map keyed by file name (e.g. `lang-csharp.instructions.md`).
     */
    readMetadata(): ReadonlyMap<string, InstructionsFileMetadata> {
        const metadata = new Map<string, InstructionsFileMetadata>();
        const instructionsDir = join(this.extensionPath, 'instructions');

        for (const fileName of readdirSync(instructionsDir)) {
            if (!fileName.endsWith('.instructions.md')) { continue; }

            const content = readFileSync(join(instructionsDir, fileName), 'utf-8');
            const { frontmatter } = InstructionsFileParser.parse(content);
            const changelogName = fileName.replace('.instructions.md', '.CHANGELOG.md');
            const hasChangelog = existsSync(join(instructionsDir, changelogName));
            if (frontmatter.description || frontmatter.version || hasChangelog) {
                metadata.set(fileName, { ...frontmatter, hasChangelog });
            }
        }

        return metadata;
    }
}
