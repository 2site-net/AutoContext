import { describe, it, expect } from 'vitest';
import { McpToolsRegistry } from '../../src/mcp-tools-registry';

describe('tools catalog', () => {
    it('should have unique setting ids', () => {
        const ids = McpToolsRegistry.all.map(t => t.settingId);

        expect.soft(new Set(ids).size).toBe(ids.length);
    });

    it('should have unique tool names', () => {
        const names = McpToolsRegistry.all.map(t => t.featureName ?? t.toolName);

        expect.soft(new Set(names).size).toBe(names.length);
    });
});
