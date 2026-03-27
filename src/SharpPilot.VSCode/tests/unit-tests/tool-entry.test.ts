import { describe, it, expect } from 'vitest';
import { toolsCatalog } from '../../src/tools-catalog';

describe('tools catalog', () => {
    it('should have unique setting ids', () => {
        const ids = toolsCatalog.all.map(t => t.settingId);

        expect(new Set(ids).size).toBe(ids.length);
    });

    it('should have unique tool names', () => {
        const names = toolsCatalog.all.map(t => t.toolName);

        expect(new Set(names).size).toBe(names.length);
    });
});
