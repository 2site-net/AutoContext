import type { McpToolsEntry } from '../../../src/types/mcp-tools-entry';

export const mcpToolsTestEntries: readonly McpToolsEntry[] = [
    { key: 'alpha', label: 'Alpha', category: 'C#', serverLabel: '.NET', scope: 'dotnet' },
    { key: 'beta', label: 'Beta', category: 'NuGet', serverLabel: '.NET', scope: 'dotnet' },
    { key: 'gamma', label: 'Gamma', category: 'Git', serverLabel: 'Workspace', scope: 'git' },
];
