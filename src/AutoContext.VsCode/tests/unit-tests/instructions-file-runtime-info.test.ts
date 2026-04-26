import { describe, it, expect } from 'vitest';
import { join } from 'node:path';
import { InstructionsFilesManifestLoader } from '../../src/instructions-files-manifest-loader';
import { McpToolsManifestLoader } from '../../src/mcp-tools-manifest-loader';

const extensionPath = join(__dirname, '..', '..');
const instructionsManifest = new InstructionsFilesManifestLoader(extensionPath).load();
const toolsManifest = new McpToolsManifestLoader(extensionPath).load();

describe('InstructionsFileRuntimeInfo.contextKey', () => {
    it('should derive the context key from the entry key', () => {
        const codeReview = instructionsManifest.findByName('code-review.instructions.md')!;

        expect.soft(codeReview.runtimeInfo.contextKey).toBe('autocontext.instructions.code-review');
    });

    it('should handle hyphenated keys', () => {
        const asyncAwait = instructionsManifest.findByName('dotnet-async-await.instructions.md')!;

        expect.soft(asyncAwait.runtimeInfo.contextKey).toBe('autocontext.instructions.dotnet-async-await');
    });
});

describe('InstructionsFileRuntimeInfo.overrideKey', () => {
    it('should derive the override key from the entry key', () => {
        const codeReview = instructionsManifest.findByName('code-review.instructions.md')!;

        expect.soft(codeReview.runtimeInfo.overrideKey).toBe('autocontext.override.code-review');
    });

    it('should handle hyphenated keys', () => {
        const asyncAwait = instructionsManifest.findByName('dotnet-async-await.instructions.md')!;

        expect.soft(asyncAwait.runtimeInfo.overrideKey).toBe('autocontext.override.dotnet-async-await');
    });
});

describe('InstructionsFileEntry.activationFlags', () => {
    it('should be empty for always-on instructions', () => {
        const codeReview = instructionsManifest.findByName('code-review.instructions.md')!;

        expect.soft(codeReview.activationFlags).toEqual([]);
    });

    it('should expose context keys for workspace-specific instructions', () => {
        const asyncAwait = instructionsManifest.findByName('dotnet-async-await.instructions.md')!;

        expect.soft(asyncAwait.activationFlags).toEqual(['hasDotNet']);
    });

    it('should expose multiple context keys for OR conditions', () => {
        const js = instructionsManifest.findByName('lang-javascript.instructions.md')!;

        expect.soft(js.activationFlags).toEqual(['hasJavaScript', 'hasTypeScript']);
    });

    it('should be exposed for tools', () => {
        const codingStyleTool = toolsManifest.toolByName('analyze_csharp_code')!;
        const commitFormatTool = toolsManifest.toolByName('analyze_git_commit_message')!;

        expect(codingStyleTool.activationFlags).toEqual(['hasDotNet', 'hasCSharp']);
        expect.soft(commitFormatTool.activationFlags).toEqual(['hasGit']);
    });

    it('should be empty for the editorconfig tool', () => {
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
            alwaysOn.has(entry.runtimeInfo.contextKey)
                ? entry.activationFlags.length === 0
                : entry.activationFlags.length > 0,
        )).toBe(true);
    });
});
