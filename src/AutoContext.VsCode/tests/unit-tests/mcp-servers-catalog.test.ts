import { describe, it, expect } from 'vitest';
import { McpServersCatalog } from '../../src/mcp-servers-catalog';
import { mcpServersTestEntries } from './_fixtures';

describe('McpServersCatalog', () => {
    it('should expose all entries', () => {
        const catalog = new McpServersCatalog(mcpServersTestEntries);

        expect.soft(catalog.all).toEqual(mcpServersTestEntries);
    });

    it('should return empty array for empty catalog', () => {
        const catalog = new McpServersCatalog([]);

        expect.soft(catalog.all).toEqual([]);
    });
});
