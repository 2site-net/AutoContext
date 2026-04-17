import { describe, it, expect } from 'vitest';
import { McpToolsCatalog } from '../../src/mcp-tools-catalog';
import type { McpToolsEntry } from '../../src/types/mcp-tools-entry';

const testEntries: readonly McpToolsEntry[] = [
    { key: 'alpha', label: 'Alpha', category: 'C#', serverLabel: '.NET', scope: 'dotnet' },
    { key: 'beta', label: 'Beta', category: 'NuGet', serverLabel: '.NET', scope: 'dotnet' },
    { key: 'gamma', label: 'Gamma', category: 'Git', serverLabel: 'Workspace', scope: 'git' },
];

describe('McpToolsCatalog', () => {
    it('should expose all entries', () => {
        const catalog = new McpToolsCatalog(testEntries);

        expect.soft(catalog.count).toBe(3);
        expect.soft(catalog.all[0].settingId).toBe('autocontext.mcpTools.alpha');
    });

    it('should return the correct count', () => {
        const catalog = new McpToolsCatalog(testEntries);

        expect.soft(catalog.count).toBe(3);
    });

    it('should return setting ids for a matching scope', () => {
        const catalog = new McpToolsCatalog(testEntries);

        expect.soft(catalog.getSettingIdsByScope('dotnet')).toEqual(['autocontext.mcpTools.alpha', 'autocontext.mcpTools.beta']);
    });

    it('should return setting ids for git scope', () => {
        const catalog = new McpToolsCatalog(testEntries);

        expect.soft(catalog.getSettingIdsByScope('git')).toEqual(['autocontext.mcpTools.gamma']);
    });

    it('should return empty array for unknown scope', () => {
        const catalog = new McpToolsCatalog(testEntries);

        expect.soft(catalog.getSettingIdsByScope('unknown')).toEqual([]);
    });

    it('should return empty array for empty catalog', () => {
        const catalog = new McpToolsCatalog([]);

        expect.soft(catalog.all).toEqual([]);
        expect.soft(catalog.count).toBe(0);
        expect.soft(catalog.getSettingIdsByScope('dotnet')).toEqual([]);
    });

    it('should enrich entries with metadata when provided', () => {
        const metadata = new Map([
            ['alpha', { description: 'Alpha tool', version: '1.0.0' }],
            ['gamma', { description: 'Gamma tool', version: '2.0.0' }],
        ]);
        const catalog = new McpToolsCatalog(testEntries, metadata);

        expect.soft(catalog.all[0].description).toBe('Alpha tool');
        expect.soft(catalog.all[0].version).toBe('1.0.0');
        expect.soft(catalog.all[1].description).toBeUndefined();
        expect.soft(catalog.all[1].version).toBeUndefined();
        expect.soft(catalog.all[2].description).toBe('Gamma tool');
        expect.soft(catalog.all[2].version).toBe('2.0.0');
    });

    it('should leave metadata undefined when no metadata map is provided', () => {
        const catalog = new McpToolsCatalog(testEntries);

        expect.soft(catalog.all[0].description).toBeUndefined();
        expect.soft(catalog.all[0].version).toBeUndefined();
    });

    it('should return tool metadata by name', () => {
        const metadata = new Map([
            ['alpha', { description: 'Alpha tool', version: '1.0.0' }],
        ]);
        const catalog = new McpToolsCatalog(testEntries, metadata);

        expect.soft(catalog.getMetadata('alpha')).toEqual({ description: 'Alpha tool', version: '1.0.0' });
        expect.soft(catalog.getMetadata('unknown')).toBeUndefined();
    });
});
