import type { McpServerEntry } from '../../../src/types/mcp-server-entry';

export const detectorTestServers: McpServerEntry[] = [
    { label: 'DotNet', scope: 'dotnet', server: 'dotnet', contextKey: 'hasDotNet' },
    { label: 'Git', scope: 'git', server: 'workspace', contextKey: 'hasGit' },
    { label: 'EditorConfig', scope: 'editorconfig', server: 'workspace' },
    { label: 'TypeScript', scope: 'typescript', server: 'web', contextKey: 'hasTypeScript' },
];
