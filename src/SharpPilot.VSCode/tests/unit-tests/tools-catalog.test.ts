import { describe, it, expect } from 'vitest';
import { tools } from '../../src/tool-entry';
import { toolSettingsForScope } from '../../src/tools-catalog';

describe('toolSettingsForScope', () => {
    it('should return .NET tool setting ids for dotnet scope', () => {
        const result = toolSettingsForScope('dotnet');

        expect(result.length).toBeGreaterThan(0);
        expect(result.every(id => id.startsWith('sharppilot.tools.'))).toBe(true);

        const dotnetTools = tools.filter(t => t.category === '.NET Tool');
        expect(result).toEqual(dotnetTools.map(t => t.settingId));
    });

    it('should return Git tool setting ids for git scope', () => {
        const result = toolSettingsForScope('git');

        expect(result.length).toBeGreaterThan(0);

        const gitTools = tools.filter(t => t.category === 'Git Tool');
        expect(result).toEqual(gitTools.map(t => t.settingId));
    });

    it('should return EditorConfig tool setting ids for editorconfig scope', () => {
        const result = toolSettingsForScope('editorconfig');

        expect(result).toHaveLength(1);
        expect(result[0]).toBe('sharppilot.tools.get_editorconfig');
    });

    it('should return empty array for unknown scope', () => {
        expect(toolSettingsForScope('unknown')).toEqual([]);
    });
});
