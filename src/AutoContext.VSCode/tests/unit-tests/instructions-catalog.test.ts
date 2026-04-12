import { describe, it, expect } from 'vitest';
import { InstructionsCatalogEntry } from '../../src/instructions-catalog-entry';
import { InstructionsCatalog } from '../../src/instructions-catalog';

const testData = [
    { key: 'alpha', fileName: 'alpha.instructions.md', label: 'Alpha', category: 'General' },
    { key: 'beta', fileName: 'beta.instructions.md', label: 'Beta', category: '.NET' },
    { key: 'gamma', fileName: 'gamma.instructions.md', label: 'Gamma', category: 'Web' },
] as const;

describe('InstructionsCatalogEntry', () => {
    it('should map files to .github/instructions/', () => {
        const entry = new InstructionsCatalogEntry({ key: 'test', fileName: 'dotnet-testing.instructions.md', label: '', category: '' });

        expect(entry.targetPath).toBe('.github/instructions/dotnet-testing.instructions.md');
    });

    it('should compute settingId from key', () => {
        const entry = new InstructionsCatalogEntry({ key: 'lang.csharp', fileName: 'lang-csharp.instructions.md', label: 'C#', category: 'Languages' });

        expect(entry.settingId).toBe('autocontext.instructions.lang.csharp');
    });
});

describe('InstructionsCatalog', () => {
    it('should expose all entries', () => {
        const catalog = new InstructionsCatalog(testData);

        expect(catalog.all).toHaveLength(3);
        expect(catalog.all[0].settingId).toBe('autocontext.instructions.alpha');
    });

    it('should return the correct count', () => {
        const catalog = new InstructionsCatalog(testData);

        expect(catalog.count).toBe(3);
    });

    it('should find an entry by file name', () => {
        const catalog = new InstructionsCatalog(testData);

        const entry = catalog.findByFileName('beta.instructions.md');
        expect(entry).toBeDefined();
        expect(entry!.settingId).toBe('autocontext.instructions.beta');
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

    it('should enrich entries with metadata when provided', () => {
        const metadata = new Map([
            ['alpha.instructions.md', { description: 'Alpha desc', version: '1.0.0' }],
            ['gamma.instructions.md', { description: 'Gamma desc', version: '2.0.0' }],
        ]);
        const catalog = new InstructionsCatalog(testData, metadata);

        expect.soft(catalog.all[0].description).toBe('Alpha desc');
        expect.soft(catalog.all[0].version).toBe('1.0.0');
        expect.soft(catalog.all[1].description).toBeUndefined();
        expect.soft(catalog.all[1].version).toBeUndefined();
        expect.soft(catalog.all[2].description).toBe('Gamma desc');
        expect.soft(catalog.all[2].version).toBe('2.0.0');
    });

    it('should leave metadata undefined when no metadata map is provided', () => {
        const catalog = new InstructionsCatalog(testData);

        expect.soft(catalog.all[0].description).toBeUndefined();
        expect.soft(catalog.all[0].version).toBeUndefined();
    });

    it('should default hasChangelog to false when not in metadata', () => {
        const catalog = new InstructionsCatalog(testData);

        expect.soft(catalog.all[0].hasChangelog).toBe(false);
        expect.soft(catalog.all[1].hasChangelog).toBe(false);
    });

    it('should set hasChangelog from metadata', () => {
        const metadata = new Map([
            ['alpha.instructions.md', { hasChangelog: true }],
            ['gamma.instructions.md', { hasChangelog: false }],
        ]);
        const catalog = new InstructionsCatalog(testData, metadata);

        expect.soft(catalog.all[0].hasChangelog).toBe(true);
        expect.soft(catalog.all[1].hasChangelog).toBe(false);
        expect.soft(catalog.all[2].hasChangelog).toBe(false);
    });
});
