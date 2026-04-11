import { describe, it, expect } from 'vitest';
import { ContextKeys } from '../../src/context-keys';
import { instructionsFiles, mcpTools } from '../../src/ui-constants';
import { InstructionsCatalog } from '../../src/instructions-catalog';
import { McpToolsCatalog } from '../../src/mcp-tools-catalog';

const instructionsCatalog = new InstructionsCatalog(instructionsFiles);
const toolsCatalog = new McpToolsCatalog(mcpTools);

describe('ContextKeys.overrideKey', () => {
    it('should strip the settings prefix and prepend the override prefix', () => {
        expect.soft(ContextKeys.overrideKey('autocontext.instructions.copilot'))
            .toBe('autocontext.override.copilot');
    });

    it('should handle nested setting ids', () => {
        expect.soft(ContextKeys.overrideKey('autocontext.instructions.dotnet.asyncAwait'))
            .toBe('autocontext.override.dotnet.asyncAwait');
    });
});

describe('ContextKeys.forEntry', () => {
    it('should return empty array for always-on instructions', () => {
        const codeReview = instructionsCatalog.all.find(i => i.settingId === 'autocontext.instructions.codeReview')!;

        expect.soft(ContextKeys.forEntry(codeReview)).toEqual([]);
    });

    it('should return context keys for workspace-specific instructions', () => {
        const asyncAwait = instructionsCatalog.all.find(i => i.settingId === 'autocontext.instructions.dotnet.asyncAwait')!;

        expect.soft(ContextKeys.forEntry(asyncAwait)).toEqual(['hasDotNet']);
    });

    it('should return multiple context keys for OR conditions', () => {
        const js = instructionsCatalog.all.find(i => i.settingId === 'autocontext.instructions.lang.javascript')!;

        expect.soft(ContextKeys.forEntry(js)).toEqual(['hasJavaScript', 'hasTypeScript']);
    });

    it('should return context keys for tools', () => {
        const codingStyle = toolsCatalog.all.find(t => t.settingId === 'autocontext.mcpTools.check_csharp_coding_style')!;
        const commitFormat = toolsCatalog.all.find(t => t.settingId === 'autocontext.mcpTools.check_git_commit_format')!;

        expect(ContextKeys.forEntry(codingStyle)).toEqual(['hasCSharp']);
        expect.soft(ContextKeys.forEntry(commitFormat)).toEqual(['hasGit']);
    });

    it('should return empty array for the editorconfig tool', () => {
        const editorconfig = toolsCatalog.all.find(t => t.settingId === 'autocontext.mcpTools.get_editorconfig')!;

        expect.soft(ContextKeys.forEntry(editorconfig)).toEqual([]);
    });

    it('should have a mapping for every instruction with a workspace when clause', () => {
        const alwaysOn = new Set([
            'autocontext.instructions.codeReview',
            'autocontext.instructions.designPrinciples',
            'autocontext.instructions.restApiDesign',
            'autocontext.instructions.lang.sql',
        ]);

        expect(instructionsCatalog.count).toBeGreaterThan(0);
        expect.soft(instructionsCatalog.all.every(entry =>
            alwaysOn.has(entry.settingId)
                ? ContextKeys.forEntry(entry).length === 0
                : ContextKeys.forEntry(entry).length > 0,
        )).toBe(true);
    });
});
