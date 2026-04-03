import { describe, it, expect } from 'vitest';
import { McpServersCatalog } from '../../src/mcp-servers-catalog';
import type { McpServerEntry } from '../../src/mcp-server-entry';

const testEntries: readonly McpServerEntry[] = [
    { label: 'Server A', category: 'alpha', process: 'dotnet', contextKey: 'hasAlpha' },
    { label: 'Server B', category: 'beta', process: 'web' },
    { label: 'Server C', category: 'gamma', process: 'workspace', contextKey: 'hasGamma' },
];

describe('McpServersCatalog', () => {
    it('should expose all entries', () => {
        const catalog = new McpServersCatalog(testEntries);

        expect(catalog.all).toEqual(testEntries);
    });

    it('should return empty array for empty catalog', () => {
        const catalog = new McpServersCatalog([]);

        expect(catalog.all).toEqual([]);
    });
});
