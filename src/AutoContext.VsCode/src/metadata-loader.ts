import { readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { InstructionsParser } from './instructions-parser.js';
import type { InstructionsMetadataEntry } from './instructions-catalog-entry.js';

export class MetadataLoader {
    constructor(private readonly extensionPath: string) {}

    getInstructionsInfo(files: readonly { fileName: string }[]): ReadonlyMap<string, InstructionsMetadataEntry> {
        const metadata = new Map<string, InstructionsMetadataEntry>();
        const instructionsDir = join(this.extensionPath, 'instructions');

        for (const file of files) {
            const content = readFileSync(join(instructionsDir, file.fileName), 'utf-8');
            const { frontmatter } = InstructionsParser.parse(content);
            const changelogName = file.fileName.replace('.instructions.md', '.CHANGELOG.md');
            const hasChangelog = existsSync(join(instructionsDir, changelogName));
            if (frontmatter.description || frontmatter.version || hasChangelog) {
                metadata.set(file.fileName, { ...frontmatter, hasChangelog });
            }
        }

        return metadata;
    }
}
