import { describe, it, expect } from 'vitest';
import { toolsCatalog } from '../../src/tools-catalog';

describe('toolsCatalog.getSettingIdByCategory', () => {
    it('should return .NET tool setting ids for dotnet category', () => {
        const result = toolsCatalog.getSettingIdByCategory('dotnet');

        expect(result.length).toBeGreaterThan(0);
        expect(result.every(id => id.startsWith('sharppilot.tools.'))).toBe(true);

        const dotnetTools = toolsCatalog.all.filter(t => t.category === '.NET Tool');
        expect(result).toEqual(dotnetTools.map(t => t.settingId));
    });

    it('should return Git tool setting ids for git category', () => {
        const result = toolsCatalog.getSettingIdByCategory('git');

        expect(result.length).toBeGreaterThan(0);

        const gitTools = toolsCatalog.all.filter(t => t.category === 'Git Tool');
        expect(result).toEqual(gitTools.map(t => t.settingId));
    });

    it('should return EditorConfig tool setting ids for editorconfig category', () => {
        const result = toolsCatalog.getSettingIdByCategory('editorconfig');

        expect(result).toHaveLength(1);
        expect(result[0]).toBe('sharppilot.tools.get_editorconfig');
    });

    it('should return empty array for unknown category', () => {
        expect(toolsCatalog.getSettingIdByCategory('unknown')).toEqual([]);
    });
});
