import type { McpServerEntry } from './mcp-server-entry.js';
import { McpServersCatalog } from './mcp-servers-catalog.js';

export class McpServersRegistry {
    private static readonly catalog = new McpServersCatalog([
        { label: 'SharpPilot: DotNet', category: 'dotnet', process: 'dotnet', contextKey: 'hasDotNet' },
        { label: 'SharpPilot: Git', category: 'git', process: 'workspace', contextKey: 'hasGit' },
        { label: 'SharpPilot: EditorConfig', category: 'editorconfig', process: 'workspace' },
        { label: 'SharpPilot: TypeScript', category: 'typescript', process: 'web', contextKey: 'hasTypeScript' },
    ]);

    static get all(): readonly McpServerEntry[] {
        return this.catalog.all;
    }
}
