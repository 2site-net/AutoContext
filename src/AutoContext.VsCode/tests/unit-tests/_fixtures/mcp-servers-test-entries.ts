import type { McpServerEntry } from '../../../src/types/mcp-server-entry';

export const mcpServersTestEntries: readonly McpServerEntry[] = [
    { label: 'Server A', scope: 'alpha', server: 'dotnet', contextKey: 'hasAlpha' },
    { label: 'Server B', scope: 'beta', server: 'web' },
    { label: 'Server C', scope: 'gamma', server: 'workspace', contextKey: 'hasGamma' },
];
