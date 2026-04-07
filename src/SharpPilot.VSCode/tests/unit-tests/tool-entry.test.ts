import { describe, it, expect } from 'vitest';
import { mcpToolEntries } from '../../src/ui-constants';

describe('tools catalog', () => {
    it('should have unique setting ids', () => {
        const ids = mcpToolEntries.map(t => t.settingId);

        expect.soft(new Set(ids).size).toBe(ids.length);
    });

    it('should have unique tool names', () => {
        const names = mcpToolEntries.map(t => t.featureName ?? t.toolName);

        expect.soft(new Set(names).size).toBe(names.length);
    });
});
