import { describe, it, expect } from 'vitest';
import { McpServersCatalog } from '../../src/mcp-servers-catalog';
import type { McpServerEntry } from '../../src/types/mcp-server-entry';

const testEntries: readonly McpServerEntry[] = [
    { label: 'Server A', scope: 'alpha', server: 'dotnet', contextKey: 'hasAlpha' },
    { label: 'Server B', scope: 'beta', server: 'web' },
    { label: 'Server C', scope: 'gamma', server: 'workspace', contextKey: 'hasGamma' },
];

describe('McpServersCatalog', () => {
    it('should expose all entries', () => {
        const catalog = new McpServersCatalog(testEntries);

        expect.soft(catalog.all).toEqual(testEntries);
    });

    it('should return empty array for empty catalog', () => {
        const catalog = new McpServersCatalog([]);

        expect.soft(catalog.all).toEqual([]);
    });
});
