import { describe, it, expect } from 'vitest';
import { InstructionsCatalogEntry } from '../../src/instructions-catalog-entry';
import { InstructionsCatalog } from '../../src/instructions-catalog';

const testData = [
    { settingId: 'inst.alpha', fileName: 'alpha.instructions.md', label: 'Alpha', category: 'General' },
    { settingId: 'inst.beta', fileName: 'beta.instructions.md', label: 'Beta', category: '.NET' },
    { settingId: 'inst.gamma', fileName: 'gamma.instructions.md', label: 'Gamma', category: 'Web' },
] as const;

describe('InstructionsCatalogEntry', () => {
    it('should map files to .github/instructions/', () => {
        const entry = new InstructionsCatalogEntry({ settingId: '', fileName: 'dotnet-testing.instructions.md', label: '', category: '' });

        expect(entry.targetPath).toBe('.github/instructions/dotnet-testing.instructions.md');
    });
});

describe('InstructionsCatalog', () => {
    it('should expose all entries', () => {
        const catalog = new InstructionsCatalog(testData);

        expect(catalog.all).toHaveLength(3);
        expect(catalog.all[0].settingId).toBe('inst.alpha');
    });

    it('should return the correct count', () => {
        const catalog = new InstructionsCatalog(testData);

        expect(catalog.count).toBe(3);
    });

    it('should find an entry by file name', () => {
        const catalog = new InstructionsCatalog(testData);

        const entry = catalog.findByFileName('beta.instructions.md');
        expect(entry).toBeDefined();
        expect(entry!.settingId).toBe('inst.beta');
    });

    it('should return undefined for an unknown file name', () => {
        const catalog = new InstructionsCatalog(testData);

        expect(catalog.findByFileName('nonexistent.md')).toBeUndefined();
    });

    it('should have unique setting ids', () => {
        const catalog = new InstructionsCatalog(testData);
        const ids = catalog.all.map(i => i.settingId);

        expect(new Set(ids).size).toBe(ids.length);
    });

    it('should return empty results for empty catalog', () => {
        const catalog = new InstructionsCatalog([]);

        expect.soft(catalog.all).toEqual([]);
        expect.soft(catalog.count).toBe(0);
        expect.soft(catalog.findByFileName('anything.md')).toBeUndefined();
    });
});
