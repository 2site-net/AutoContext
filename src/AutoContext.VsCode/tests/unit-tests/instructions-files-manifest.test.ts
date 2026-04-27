import { describe, it, expect } from 'vitest';
import { InstructionsFileCategoryEntry } from '../../src/instructions-file-category-entry';
import { InstructionsFileEntry } from '../../src/instructions-file-entry';
import { InstructionsFilesManifest } from '../../src/instructions-files-manifest';
import { catalogTestEntries, makeInstructionsFileEntry, makeInstructionsFilesManifest } from '#testing/fixtures';

describe('InstructionsFileEntry', () => {
    it('should map files to .github/instructions/', () => {
        const entry = makeInstructionsFileEntry('dotnet-testing.instructions.md', 'Testing', ['General']);

        expect(entry.targetPath).toBe('.github/instructions/dotnet-testing.instructions.md');
    });

    it('should compute key from name (kebab-case)', () => {
        const entry = makeInstructionsFileEntry('lang-csharp.instructions.md', 'C#', ['Languages']);

        expect(entry.key).toBe('lang-csharp');
        expect(entry.runtimeInfo.contextKey).toBe('autocontext.instructions.lang-csharp');
    });

    it('should expose firstCategory', () => {
        const cats = [new InstructionsFileCategoryEntry('A'), new InstructionsFileCategoryEntry('B')];
        const entry = new InstructionsFileEntry('x.instructions.md', 'X', cats);

        expect(entry.firstCategory.name).toBe('A');
    });
});

describe('InstructionsFilesManifest', () => {
    it('should expose all instructions', () => {
        const manifest = makeInstructionsFilesManifest(catalogTestEntries);

        expect(manifest.instructions).toHaveLength(3);
        expect(manifest.instructions[0].runtimeInfo.contextKey).toBe('autocontext.instructions.alpha');
    });

    it('should return the correct count', () => {
        const manifest = makeInstructionsFilesManifest(catalogTestEntries);

        expect(manifest.count).toBe(3);
    });

    it('should find an entry by name', () => {
        const manifest = makeInstructionsFilesManifest(catalogTestEntries);

        const entry = manifest.findByName('beta.instructions.md');
        expect(entry).toBeDefined();
        expect(entry!.runtimeInfo.contextKey).toBe('autocontext.instructions.beta');
    });

    it('should return undefined for an unknown name', () => {
        const manifest = makeInstructionsFilesManifest(catalogTestEntries);

        expect(manifest.findByName('nonexistent.md')).toBeUndefined();
    });

    it('should have unique context keys', () => {
        const manifest = makeInstructionsFilesManifest(catalogTestEntries);
        const ids = manifest.instructions.map(i => i.runtimeInfo.contextKey);

        expect(new Set(ids).size).toBe(ids.length);
    });

    it('should return empty results for empty manifest', () => {
        const manifest = new InstructionsFilesManifest([], []);

        expect.soft(manifest.instructions).toEqual([]);
        expect.soft(manifest.count).toBe(0);
        expect.soft(manifest.findByName('anything.md')).toBeUndefined();
    });

    it('should default hasChangelog to false when no metadata supplied', () => {
        const manifest = makeInstructionsFilesManifest(catalogTestEntries);

        expect.soft(manifest.instructions[0].hasChangelog).toBe(false);
    });

    it('should set version, description, and hasChangelog from metadata', () => {
        const cats = [new InstructionsFileCategoryEntry('General')];
        const entry = new InstructionsFileEntry(
            'alpha.instructions.md',
            'Alpha',
            cats,
            undefined,
            { description: 'Alpha desc', version: '1.0.0', hasChangelog: true },
        );

        expect.soft(entry.description).toBe('Alpha desc');
        expect.soft(entry.version).toBe('1.0.0');
        expect.soft(entry.hasChangelog).toBe(true);
    });
});
