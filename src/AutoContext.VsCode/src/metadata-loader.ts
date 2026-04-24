import { readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { InstructionsParser } from './instructions-parser.js';
import type { McpToolsMetadataEntry } from './mcp-tools-catalog-entry.js';
import type { InstructionsMetadataEntry } from './instructions-catalog-entry.js';

interface ManifestTool {
    name: string;
    description: string;
    version: string;
    tasks?: ManifestTool[];
}

export class MetadataLoader {
    constructor(private readonly extensionPath: string) {}

    getMcpToolsInfo(): ReadonlyMap<string, McpToolsMetadataEntry> {
        const manifest: Record<string, ManifestTool[]> = JSON.parse(
            readFileSync(join(this.extensionPath, '.mcp-tools.json'), 'utf-8'),
        );
        const metadata = new Map<string, McpToolsMetadataEntry>();

        for (const tools of Object.values(manifest)) {
            for (const tool of tools) {
                metadata.set(tool.name, { description: tool.description, version: tool.version });
                if (tool.tasks) {
                    for (const task of tool.tasks) {
                        metadata.set(task.name, { description: task.description, version: task.version });
                    }
                }
            }
        }

        return metadata;
    }

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
