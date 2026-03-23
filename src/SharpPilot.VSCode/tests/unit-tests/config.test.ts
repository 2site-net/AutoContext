import { describe, it, expect } from 'vitest';
import {
    overrideContextKey,
    filteredContextKey,
    targetPath,
    instructionByFileName,
    instructions,
    tools,
    servers,
    toolSettingsForScope,
    contextKeysForEntry,
    type InstructionEntry,
} from '../../src/config';

describe('overrideContextKey', () => {
    it('should strip the settings prefix and prepend the override prefix', () => {
        expect(overrideContextKey('sharp-pilot.instructions.copilot'))
            .toBe('sharp-pilot.override.copilot');
    });

    it('should handle nested setting ids', () => {
        expect(overrideContextKey('sharp-pilot.instructions.dotnet.asyncAwait'))
            .toBe('sharp-pilot.override.dotnet.asyncAwait');
    });
});

describe('filteredContextKey', () => {
    it('should strip the settings prefix and prepend the filtered prefix', () => {
        expect(filteredContextKey('sharp-pilot.instructions.copilot'))
            .toBe('sharp-pilot.filtered.copilot');
    });

    it('should handle nested setting ids', () => {
        expect(filteredContextKey('sharp-pilot.instructions.dotnet.asyncAwait'))
            .toBe('sharp-pilot.filtered.dotnet.asyncAwait');
    });
});

describe('targetPath', () => {
    it('should map copilot.instructions.md to .github/copilot-instructions.md', () => {
        const entry = { fileName: 'copilot.instructions.md' } as InstructionEntry;

        expect(targetPath(entry)).toBe('.github/copilot-instructions.md');
    });

    it('should map other files to .github/instructions/', () => {
        const entry = { fileName: 'dotnet-testing.instructions.md' } as InstructionEntry;

        expect(targetPath(entry)).toBe('.github/instructions/dotnet-testing.instructions.md');
    });
});

describe('instructionByFileName', () => {
    it('should return the entry for a known file name', () => {
        const entry = instructionByFileName('copilot.instructions.md');

        expect(entry).toBeDefined();
        expect(entry!.settingId).toBe('sharp-pilot.instructions.copilot');
    });

    it('should return undefined for an unknown file name', () => {
        expect(instructionByFileName('nonexistent.md')).toBeUndefined();
    });
});

describe('config arrays', () => {
    it('should have unique instruction setting ids', () => {
        const ids = instructions.map(i => i.settingId);

        expect(new Set(ids).size).toBe(ids.length);
    });

    it('should have unique tool setting ids', () => {
        const ids = tools.map(t => t.settingId);

        expect(new Set(ids).size).toBe(ids.length);
    });

    it('should have unique tool names', () => {
        const names = tools.map(t => t.toolName);

        expect(new Set(names).size).toBe(names.length);
    });

    it('should have unique server scopes', () => {
        const scopes = servers.map(s => s.scope);

        expect(new Set(scopes).size).toBe(scopes.length);
    });

    it('should have .instructions.md suffix on all instruction file names', () => {
        for (const entry of instructions) {
            expect(entry.fileName).toMatch(/\.instructions\.md$/);
        }
    });

    it('should have a contextKey on workspace-specific server entries', () => {
        const dotnet = servers.find(s => s.scope === 'dotnet')!;
        const git = servers.find(s => s.scope === 'git')!;

        expect(dotnet.contextKey).toBe('hasDotnet');
        expect(git.contextKey).toBe('hasGit');
    });

    it('should not require a contextKey on the editorconfig server', () => {
        const editorconfig = servers.find(s => s.scope === 'editorconfig')!;

        expect(editorconfig.contextKey).toBeUndefined();
    });
});

describe('toolSettingsForScope', () => {
    it('should return .NET tool setting ids for dotnet scope', () => {
        const result = toolSettingsForScope('dotnet');

        expect(result.length).toBeGreaterThan(0);
        expect(result.every(id => id.startsWith('sharp-pilot.tools.'))).toBe(true);

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
        expect(result[0]).toBe('sharp-pilot.tools.get_editorconfig');
    });

    it('should return empty array for unknown scope', () => {
        expect(toolSettingsForScope('unknown')).toEqual([]);
    });
});

describe('contextKeysForEntry', () => {
    it('should return empty array for always-on instructions', () => {
        const copilot = instructions.find(i => i.settingId === 'sharp-pilot.instructions.copilot')!;

        expect(contextKeysForEntry(copilot)).toEqual([]);
    });

    it('should return context keys for workspace-specific instructions', () => {
        const asyncAwait = instructions.find(i => i.settingId === 'sharp-pilot.instructions.dotnet.asyncAwait')!;

        expect(contextKeysForEntry(asyncAwait)).toEqual(['hasDotnet']);
    });

    it('should return multiple context keys for OR conditions', () => {
        const js = instructions.find(i => i.settingId === 'sharp-pilot.instructions.web.javascript')!;

        expect(contextKeysForEntry(js)).toEqual(['hasJavaScript', 'hasTypeScript']);
    });

    it('should return context keys for tools', () => {
        const codingStyle = tools.find(t => t.settingId === 'sharp-pilot.tools.check_csharp_coding_style')!;
        const commitFormat = tools.find(t => t.settingId === 'sharp-pilot.tools.check_git_commit_format')!;

        expect(contextKeysForEntry(codingStyle)).toEqual(['hasDotnet']);
        expect(contextKeysForEntry(commitFormat)).toEqual(['hasGit']);
    });

    it('should return empty array for the editorconfig tool', () => {
        const editorconfig = tools.find(t => t.settingId === 'sharp-pilot.tools.get_editorconfig')!;

        expect(contextKeysForEntry(editorconfig)).toEqual([]);
    });

    it('should have a mapping for every instruction with a workspace when clause', () => {
        const alwaysOn = new Set([
            'sharp-pilot.instructions.copilot',
            'sharp-pilot.instructions.codeReview',
            'sharp-pilot.instructions.designPrinciples',
            'sharp-pilot.instructions.restApiDesign',
            'sharp-pilot.instructions.sql',
        ]);

        for (const entry of instructions) {
            if (alwaysOn.has(entry.settingId)) {
                expect(contextKeysForEntry(entry)).toEqual([]);
            } else {
                expect(contextKeysForEntry(entry).length).toBeGreaterThan(0);
            }
        }
    });
});
