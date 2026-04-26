import { describe, it, expect } from 'vitest';
import { join } from 'node:path';
import { ContextKeys } from '../../src/context-keys';
import { InstructionsFilesManifestLoader } from '../../src/instructions-files-manifest-loader';
import { McpToolsManifestLoader } from '../../src/mcp-tools-manifest-loader';

const extensionPath = join(__dirname, '..', '..');
const instructionsManifest = new InstructionsFilesManifestLoader(extensionPath).load();
const toolsManifest = new McpToolsManifestLoader(extensionPath).load();

describe('ContextKeys.overrideKey', () => {
    it('should strip the settings prefix and prepend the override prefix', () => {
        expect.soft(ContextKeys.overrideKey('autocontext.instructions.copilot'))
            .toBe('autocontext.override.copilot');
    });

    it('should handle nested setting ids', () => {
        expect.soft(ContextKeys.overrideKey('autocontext.instructions.dotnet-async-await'))
            .toBe('autocontext.override.dotnet-async-await');
    });
});

describe('ContextKeys.forEntry', () => {
    it('should return empty array for always-on instructions', () => {
        const codeReview = instructionsManifest.findByName('code-review.instructions.md')!;

        expect.soft(ContextKeys.forEntry(codeReview)).toEqual([]);
    });

    it('should return context keys for workspace-specific instructions', () => {
        const asyncAwait = instructionsManifest.findByName('dotnet-async-await.instructions.md')!;

        expect.soft(ContextKeys.forEntry(asyncAwait)).toEqual(['hasDotNet']);
    });

    it('should return multiple context keys for OR conditions', () => {
        const js = instructionsManifest.findByName('lang-javascript.instructions.md')!;

        expect.soft(ContextKeys.forEntry(js)).toEqual(['hasJavaScript', 'hasTypeScript']);
    });

    it('should return context keys for tools', () => {
        const codingStyleTool = toolsManifest.toolByName('analyze_csharp_code')!;
        const commitFormatTool = toolsManifest.toolByName('analyze_git_commit_message')!;

        expect(codingStyleTool.activationFlags).toEqual(['hasDotNet', 'hasCSharp']);
        expect.soft(commitFormatTool.activationFlags).toEqual(['hasGit']);
    });

    it('should return empty array for the editorconfig tool', () => {
        const editorconfigTool = toolsManifest.toolByName('read_editorconfig_properties')!;

        expect.soft(editorconfigTool.activationFlags).toEqual([]);
    });

    it('should have a mapping for every instruction with a workspace when clause', () => {
        const alwaysOn = new Set([
            'autocontext.instructions.code-review',
            'autocontext.instructions.design-principles',
            'autocontext.instructions.rest-api-design',
            'autocontext.instructions.lang-sql',
        ]);

        expect(instructionsManifest.count).toBeGreaterThan(0);
        expect.soft(instructionsManifest.instructions.every(entry =>
            alwaysOn.has(entry.contextKey)
                ? ContextKeys.forEntry(entry).length === 0
                : ContextKeys.forEntry(entry).length > 0,
        )).toBe(true);
    });
});
