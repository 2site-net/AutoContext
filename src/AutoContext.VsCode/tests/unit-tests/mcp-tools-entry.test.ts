import { describe, it, expect } from 'vitest';
import { mcpTools } from '../../src/ui-constants';
import { McpToolsCatalog } from '../../src/mcp-tools-catalog';

const catalog = new McpToolsCatalog(mcpTools);

describe('tools catalog', () => {
    it('should have unique setting ids', () => {
        const ids = catalog.all.map(t => t.contextKey);

        expect.soft(new Set(ids).size).toBe(ids.length);
    });

    it('should have unique keys', () => {
        const keys = mcpTools.map(t => t.key);

        expect.soft(new Set(keys).size).toBe(keys.length);
    });
});
