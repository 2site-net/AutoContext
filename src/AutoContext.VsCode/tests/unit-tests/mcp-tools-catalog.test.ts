import { describe, it, expect } from 'vitest';
import { McpToolsCatalog } from '../../src/mcp-tools-catalog';
import { mcpToolsTestEntries } from './_fixtures';

describe('McpToolsCatalog', () => {
    it('should expose all entries', () => {
        const catalog = new McpToolsCatalog(mcpToolsTestEntries);

        expect.soft(catalog.count).toBe(3);
        expect.soft(catalog.all[0].contextKey).toBe('autocontext.mcpTools.alpha');
    });

    it('should return the correct count', () => {
        const catalog = new McpToolsCatalog(mcpToolsTestEntries);

        expect.soft(catalog.count).toBe(3);
    });

    it('should return empty arrays for empty catalog', () => {
        const catalog = new McpToolsCatalog([]);

        expect.soft(catalog.all).toEqual([]);
        expect.soft(catalog.count).toBe(0);
    });

    it('should derive serverLabelOrder from entries when not supplied', () => {
        const catalog = new McpToolsCatalog(mcpToolsTestEntries);

        expect.soft([...catalog.serverLabelOrder]).toEqual(['.NET', 'Workspace']);
    });

    it('should derive categoryOrder from entries when not supplied', () => {
        const catalog = new McpToolsCatalog(mcpToolsTestEntries);

        expect.soft([...catalog.categoryOrder]).toEqual(['C#', 'NuGet', 'Git']);
    });

    it('should accept supplied ordering and worker map', () => {
        const catalog = new McpToolsCatalog(mcpToolsTestEntries, {
            serverLabelOrder: ['Workspace', '.NET'],
            categoryOrder: ['Git', 'C#', 'NuGet'],
            serverLabelToWorkerIdMap: new Map([['.NET', 'dotnet'], ['Workspace', 'workspace']]),
        });

        expect.soft([...catalog.serverLabelOrder]).toEqual(['Workspace', '.NET']);
        expect.soft([...catalog.categoryOrder]).toEqual(['Git', 'C#', 'NuGet']);
        expect.soft(catalog.serverLabelToWorkerIdMap.get('.NET')).toBe('dotnet');
    });

    it('should enrich entries with metadata when provided', () => {
        const metadata = new Map([
            ['alpha', { description: 'Alpha tool' }],
            ['gamma', { description: 'Gamma tool' }],
        ]);
        const catalog = new McpToolsCatalog(mcpToolsTestEntries, { metadata });

        expect.soft(catalog.all[0].description).toBe('Alpha tool');
        expect.soft(catalog.all[1].description).toBeUndefined();
        expect.soft(catalog.all[2].description).toBe('Gamma tool');
    });

    it('should leave metadata undefined when no metadata map is provided', () => {
        const catalog = new McpToolsCatalog(mcpToolsTestEntries);

        expect.soft(catalog.all[0].description).toBeUndefined();
    });

    it('should return tool metadata by name', () => {
        const metadata = new Map([
            ['alpha', { description: 'Alpha tool' }],
        ]);
        const catalog = new McpToolsCatalog(mcpToolsTestEntries, { metadata });

        expect.soft(catalog.getMetadata('alpha')).toEqual({ description: 'Alpha tool' });
        expect.soft(catalog.getMetadata('unknown')).toBeUndefined();
    });
});
