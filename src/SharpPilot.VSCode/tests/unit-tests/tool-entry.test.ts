import { describe, it, expect } from 'vitest';
import { mcpTools } from '../../src/ui-constants';

describe('tools catalog', () => {
    it('should have unique setting ids', () => {
        const ids = mcpTools.map(t => t.settingId);

        expect.soft(new Set(ids).size).toBe(ids.length);
    });

    it('should have unique tool names', () => {
        const names = mcpTools.map(t => t.featureName ?? t.toolName);

        expect.soft(new Set(names).size).toBe(names.length);
    });
});
