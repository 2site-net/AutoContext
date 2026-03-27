import { describe, it, expect } from 'vitest';
import {
    targetPath,
    instructionByFileName,
    instructions,
    type InstructionEntry,
} from '../../src/instructions-catalog';

describe('targetPath', () => {
    it('should map files to .github/instructions/', () => {
        const entry = { fileName: 'dotnet-testing.instructions.md' } as InstructionEntry;

        expect(targetPath(entry)).toBe('.github/instructions/dotnet-testing.instructions.md');
    });
});

describe('instructionByFileName', () => {
    it('should return the entry for a known file name', () => {
        const entry = instructionByFileName('code-review.instructions.md');

        expect(entry).toBeDefined();
        expect(entry!.settingId).toBe('sharppilot.instructions.codeReview');
    });

    it('should return undefined for an unknown file name', () => {
        expect(instructionByFileName('nonexistent.md')).toBeUndefined();
    });

    it('should not include copilot.instructions.md (always-on, not toggleable)', () => {
        expect(instructionByFileName('copilot.instructions.md')).toBeUndefined();
    });
});

describe('instructions catalog', () => {
    it('should have unique setting ids', () => {
        const ids = instructions.map(i => i.settingId);

        expect(new Set(ids).size).toBe(ids.length);
    });

    it('should have .instructions.md suffix on all file names', () => {
        for (const entry of instructions) {
            expect(entry.fileName).toMatch(/\.instructions\.md$/);
        }
    });
});
