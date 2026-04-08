import { describe, it, expect } from 'vitest';
import { McpToolsCatalog } from '../../src/mcp-tools-catalog';
import type { McpToolEntry } from '../../src/mcp-tool-entry';

const testEntries: readonly McpToolEntry[] = [
    { key: 'alpha', label: 'Alpha', category: 'C#', group: '.NET', serverCategory: 'dotnet' },
    { key: 'beta', label: 'Beta', category: 'NuGet', group: '.NET', serverCategory: 'dotnet' },
    { key: 'gamma', label: 'Gamma', category: 'Git', group: 'Workspace', serverCategory: 'git' },
];

describe('McpToolsCatalog', () => {
    it('should expose all entries', () => {
        const catalog = new McpToolsCatalog(testEntries);

        expect.soft(catalog.count).toBe(3);
        expect.soft(catalog.all[0].settingId).toBe('sharppilot.tools.alpha');
    });

    it('should return the correct count', () => {
        const catalog = new McpToolsCatalog(testEntries);

        expect.soft(catalog.count).toBe(3);
    });

    it('should return setting ids for a matching server category', () => {
        const catalog = new McpToolsCatalog(testEntries);

        expect.soft(catalog.getSettingIdByCategory('dotnet')).toEqual(['sharppilot.tools.alpha', 'sharppilot.tools.beta']);
    });

    it('should return setting ids for git server category', () => {
        const catalog = new McpToolsCatalog(testEntries);

        expect.soft(catalog.getSettingIdByCategory('git')).toEqual(['sharppilot.tools.gamma']);
    });

    it('should return empty array for unknown server category', () => {
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
