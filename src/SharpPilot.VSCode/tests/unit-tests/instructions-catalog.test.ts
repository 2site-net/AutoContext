import { describe, it, expect } from 'vitest';
import { InstructionsCatalogEntry } from '../../src/instructions-catalog-entry';
import { instructionsCatalog } from '../../src/instructions-catalog';

describe('targetPath', () => {
    it('should map files to .github/instructions/', () => {
        const entry = new InstructionsCatalogEntry({ settingId: '', fileName: 'dotnet-testing.instructions.md', label: '', category: '' });

        expect(entry.targetPath).toBe('.github/instructions/dotnet-testing.instructions.md');
    });
});

describe('findByFileName', () => {
    it('should return the entry for a known file name', () => {
        const entry = instructionsCatalog.findByFileName('code-review.instructions.md');

        expect(entry).toBeDefined();
        expect(entry!.settingId).toBe('sharppilot.instructions.codeReview');
    });

    it('should return undefined for an unknown file name', () => {
        expect(instructionsCatalog.findByFileName('nonexistent.md')).toBeUndefined();
    });

    it('should not include copilot.instructions.md (always-on, not toggleable)', () => {
        expect(instructionsCatalog.findByFileName('copilot.instructions.md')).toBeUndefined();
    });
});

describe('instructions catalog', () => {
    it('should have unique setting ids', () => {
        const ids = instructionsCatalog.all.map(i => i.settingId);

        expect(new Set(ids).size).toBe(ids.length);
    });

    it('should have .instructions.md suffix on all file names', () => {
        for (const entry of instructionsCatalog.all) {
            expect(entry.fileName).toMatch(/\.instructions\.md$/);
        }
    });
});
