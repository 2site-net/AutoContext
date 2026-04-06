import { describe, it, expect } from 'vitest';
import { McpToolsCatalog } from '../../src/mcp-tools-catalog';
import type { McpToolEntry } from '../../src/mcp-tool-entry';

const testEntries: readonly McpToolEntry[] = [
    { settingId: 'tools.alpha', toolName: 'alpha', aggregationTool: 'alpha', label: 'Alpha', category: 'C#', group: '.NET' },
    { settingId: 'tools.beta', toolName: 'beta', aggregationTool: 'beta', label: 'Beta', category: 'NuGet', group: '.NET' },
    { settingId: 'tools.gamma', toolName: 'gamma', aggregationTool: 'gamma', label: 'Gamma', category: 'Git', group: 'Workspace' },
];

describe('McpToolsCatalog', () => {
    it('should expose all entries', () => {
        const catalog = new McpToolsCatalog(testEntries);

        expect.soft(catalog.all).toEqual(testEntries);
    });

    it('should return the correct count', () => {
        const catalog = new McpToolsCatalog(testEntries);

        expect.soft(catalog.count).toBe(3);
    });

    it('should return setting ids for a matching category', () => {
        const catalog = new McpToolsCatalog(testEntries);

        expect.soft(catalog.getSettingIdByCategory('dotnet')).toEqual(['tools.alpha', 'tools.beta']);
    });

    it('should return setting ids for git category', () => {
        const catalog = new McpToolsCatalog(testEntries);

        expect.soft(catalog.getSettingIdByCategory('git')).toEqual(['tools.gamma']);
    });

    it('should return empty array for unknown category', () => {
        const catalog = new McpToolsCatalog(testEntries);

        expect.soft(catalog.getSettingIdByCategory('unknown')).toEqual([]);
    });

    it('should return empty array for empty catalog', () => {
        const catalog = new McpToolsCatalog([]);

        expect.soft(catalog.all).toEqual([]);
        expect.soft(catalog.count).toBe(0);
        expect.soft(catalog.getSettingIdByCategory('dotnet')).toEqual([]);
    });
});
