import { describe, it, expect } from 'vitest';
import { join } from 'node:path';
import { McpToolsManifestLoader } from '../../src/mcp-tools-manifest-loader';

const manifest = new McpToolsManifestLoader(join(__dirname, '..', '..')).load();

describe('tools manifest', () => {
    it('should have unique tool names', () => {
        const names = manifest.tools.map(t => t.name);

        expect.soft(new Set(names).size).toBe(names.length);
    });

    it('should have unique task context keys across all tools', () => {
        const keys = manifest.tools.flatMap(t =>
            t.tasks.length === 0 ? [t.runtimeInfo.contextKey] : t.tasks.map(task => task.runtimeInfo.contextKey),
        );

        expect.soft(new Set(keys).size).toBe(keys.length);
    });
});
