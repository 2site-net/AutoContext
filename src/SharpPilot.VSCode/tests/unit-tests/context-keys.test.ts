import { describe, it, expect } from 'vitest';
import { ContextKeys } from '../../src/context-keys';
import { InstructionsRegistry } from '../../src/instructions-registry';
import { McpToolsRegistry } from '../../src/mcp-tools-registry';

describe('ContextKeys.overrideKey', () => {
    it('should strip the settings prefix and prepend the override prefix', () => {
        expect.soft(ContextKeys.overrideKey('sharppilot.instructions.copilot'))
            .toBe('sharppilot.override.copilot');
    });

    it('should handle nested setting ids', () => {
        expect.soft(ContextKeys.overrideKey('sharppilot.instructions.dotnet.asyncAwait'))
            .toBe('sharppilot.override.dotnet.asyncAwait');
    });
});

describe('ContextKeys.forEntry', () => {
    it('should return empty array for always-on instructions', () => {
        const codeReview = InstructionsRegistry.all.find(i => i.settingId === 'sharppilot.instructions.codeReview')!;

        expect.soft(ContextKeys.forEntry(codeReview)).toEqual([]);
    });

    it('should return context keys for workspace-specific instructions', () => {
        const asyncAwait = InstructionsRegistry.all.find(i => i.settingId === 'sharppilot.instructions.dotnet.asyncAwait')!;

        expect.soft(ContextKeys.forEntry(asyncAwait)).toEqual(['hasDotNet']);
    });

    it('should return multiple context keys for OR conditions', () => {
        const js = InstructionsRegistry.all.find(i => i.settingId === 'sharppilot.instructions.lang.javascript')!;

        expect.soft(ContextKeys.forEntry(js)).toEqual(['hasJavaScript', 'hasTypeScript']);
    });

    it('should return context keys for tools', () => {
        const codingStyle = McpToolsRegistry.all.find(t => t.settingId === 'sharppilot.tools.check_csharp_coding_style')!;
        const commitFormat = McpToolsRegistry.all.find(t => t.settingId === 'sharppilot.tools.check_git_commit_format')!;

        expect(ContextKeys.forEntry(codingStyle)).toEqual(['hasCSharp']);
        expect.soft(ContextKeys.forEntry(commitFormat)).toEqual(['hasGit']);
    });

    it('should return empty array for the editorconfig tool', () => {
        const editorconfig = McpToolsRegistry.all.find(t => t.settingId === 'sharppilot.tools.get_editorconfig')!;

        expect.soft(ContextKeys.forEntry(editorconfig)).toEqual([]);
    });

    it('should have a mapping for every instruction with a workspace when clause', () => {
        const alwaysOn = new Set([
            'sharppilot.instructions.codeReview',
            'sharppilot.instructions.designPrinciples',
            'sharppilot.instructions.restApiDesign',
            'sharppilot.instructions.lang.sql',
        ]);

        expect(InstructionsRegistry.all.length).toBeGreaterThan(0);
        expect.soft(InstructionsRegistry.all.every(entry =>
            alwaysOn.has(entry.settingId)
                ? ContextKeys.forEntry(entry).length === 0
                : ContextKeys.forEntry(entry).length > 0,
        )).toBe(true);
    });
});
