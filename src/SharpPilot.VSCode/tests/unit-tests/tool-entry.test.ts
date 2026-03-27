import { describe, it, expect } from 'vitest';
import { tools } from '../../src/tool-entry';

describe('tools catalog', () => {
    it('should have unique setting ids', () => {
        const ids = tools.map(t => t.settingId);

        expect(new Set(ids).size).toBe(ids.length);
    });

    it('should have unique tool names', () => {
        const names = tools.map(t => t.toolName);

        expect(new Set(names).size).toBe(names.length);
    });
});
