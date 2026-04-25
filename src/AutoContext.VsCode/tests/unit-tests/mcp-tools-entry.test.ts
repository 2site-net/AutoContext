import { describe, it, expect } from 'vitest';
import { join } from 'node:path';
import { McpToolsCatalog } from '../../src/mcp-tools-catalog';
import { McpToolsManifestLoader } from '../../src/mcp-tools-manifest-loader';

const manifest = new McpToolsManifestLoader(join(__dirname, '..', '..')).load();
const catalog = new McpToolsCatalog(manifest);

describe('tools catalog', () => {
    it('should have unique setting ids', () => {
        const ids = catalog.all.map(t => t.contextKey);

        expect.soft(new Set(ids).size).toBe(ids.length);
    });

    it('should have unique catalog entry keys', () => {
        const keys = catalog.all.map(t => t.contextKey);

        expect.soft(new Set(keys).size).toBe(keys.length);
    });
});
