import { describe, it, expect } from 'vitest';
import { overrideContextKey, contextKeysForEntry } from '../../src/toggle-context-keys';
import { instructionsCatalog } from '../../src/instructions-catalog';
import { toolsCatalog } from '../../src/tools-catalog';

describe('overrideContextKey', () => {
    it('should strip the settings prefix and prepend the override prefix', () => {
        expect(overrideContextKey('sharppilot.instructions.copilot'))
            .toBe('sharppilot.override.copilot');
    });

    it('should handle nested setting ids', () => {
        expect(overrideContextKey('sharppilot.instructions.dotnet.asyncAwait'))
            .toBe('sharppilot.override.dotnet.asyncAwait');
    });
});

describe('contextKeysForEntry', () => {
    it('should return empty array for always-on instructions', () => {
        const codeReview = instructionsCatalog.all.find(i => i.settingId === 'sharppilot.instructions.codeReview')!;

        expect(contextKeysForEntry(codeReview)).toEqual([]);
    });

    it('should return context keys for workspace-specific instructions', () => {
        const asyncAwait = instructionsCatalog.all.find(i => i.settingId === 'sharppilot.instructions.dotnet.asyncAwait')!;

        expect(contextKeysForEntry(asyncAwait)).toEqual(['hasDotnet']);
    });

    it('should return multiple context keys for OR conditions', () => {
        const js = instructionsCatalog.all.find(i => i.settingId === 'sharppilot.instructions.web.javascript')!;

        expect(contextKeysForEntry(js)).toEqual(['hasJavaScript', 'hasTypeScript']);
    });

    it('should return context keys for tools', () => {
        const codingStyle = toolsCatalog.all.find(t => t.settingId === 'sharppilot.tools.check_csharp_coding_style')!;
        const commitFormat = toolsCatalog.all.find(t => t.settingId === 'sharppilot.tools.check_git_commit_format')!;

        expect(contextKeysForEntry(codingStyle)).toEqual(['hasDotnet']);
        expect(contextKeysForEntry(commitFormat)).toEqual(['hasGit']);
    });

    it('should return empty array for the editorconfig tool', () => {
        const editorconfig = toolsCatalog.all.find(t => t.settingId === 'sharppilot.tools.get_editorconfig')!;

        expect(contextKeysForEntry(editorconfig)).toEqual([]);
    });

    it('should have a mapping for every instruction with a workspace when clause', () => {
        const alwaysOn = new Set([
            'sharppilot.instructions.codeReview',
            'sharppilot.instructions.designPrinciples',
            'sharppilot.instructions.restApiDesign',
            'sharppilot.instructions.sql',
        ]);

        for (const entry of instructionsCatalog.all) {
            if (alwaysOn.has(entry.settingId)) {
                expect(contextKeysForEntry(entry)).toEqual([]);
            } else {
                expect(contextKeysForEntry(entry).length).toBeGreaterThan(0);
            }
        }
    });
});
