import { describe, it, expect } from 'vitest';
import {
    overrideContextKey,
    targetPath,
    instructionByFileName,
    instructions,
    tools,
    servers,
    type InstructionEntry,
} from '../src/config';

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
});
