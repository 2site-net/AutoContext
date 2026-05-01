import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { MetadataLoader } from '#src/metadata-loader';

describe('MetadataLoader.getInstructionsInfo', () => {
    let root: string;
    let instructionsDir: string;

    beforeEach(() => {
        root = mkdtempSync(join(tmpdir(), 'autocontext-metadata-loader-'));
        instructionsDir = join(root, 'instructions');
        mkdirSync(instructionsDir, { recursive: true });
    });

    afterEach(() => {
        rmSync(root, { recursive: true, force: true });
    });

    it('includes a file with frontmatter description', () => {
        writeFileSync(
            join(instructionsDir, 'a.instructions.md'),
            '---\ndescription: "Alpha"\n---\nbody\n',
        );

        const map = new MetadataLoader(root).getInstructionsInfo();

        expect(map.size).toBe(1);
        expect(map.get('a.instructions.md')).toMatchObject({ description: 'Alpha', hasChangelog: false });
    });

    it('includes a file with frontmatter version (parsed from name suffix)', () => {
        writeFileSync(
            join(instructionsDir, 'b.instructions.md'),
            '---\nname: "Beta (v1.2.3)"\n---\nbody\n',
        );

        const map = new MetadataLoader(root).getInstructionsInfo();

        expect(map.get('b.instructions.md')).toMatchObject({ version: '1.2.3', hasChangelog: false });
    });

    it('includes a file with a sibling CHANGELOG and sets hasChangelog=true', () => {
        writeFileSync(
            join(instructionsDir, 'c.instructions.md'),
            '---\n---\nbody\n',
        );
        writeFileSync(join(instructionsDir, 'c.CHANGELOG.md'), '# changes\n');

        const map = new MetadataLoader(root).getInstructionsInfo();

        expect(map.has('c.instructions.md')).toBe(true);
        expect(map.get('c.instructions.md')!.hasChangelog).toBe(true);
    });

    it('excludes a file with neither metadata nor a changelog', () => {
        writeFileSync(
            join(instructionsDir, 'd.instructions.md'),
            'no frontmatter at all\n',
        );

        const map = new MetadataLoader(root).getInstructionsInfo();

        expect(map.has('d.instructions.md')).toBe(false);
    });

    it('ignores files that do not end in .instructions.md', () => {
        writeFileSync(join(instructionsDir, 'README.md'), '# readme\n');
        writeFileSync(
            join(instructionsDir, 'e.instructions.md'),
            '---\ndescription: "Echo"\n---\n',
        );

        const map = new MetadataLoader(root).getInstructionsInfo();

        expect(Array.from(map.keys())).toEqual(['e.instructions.md']);
    });

    it('returns a multi-entry map for several qualifying files', () => {
        writeFileSync(join(instructionsDir, 'a.instructions.md'), '---\ndescription: "A"\n---\n');
        writeFileSync(join(instructionsDir, 'b.instructions.md'), '---\nname: "B (v0.1.0)"\n---\n');

        const map = new MetadataLoader(root).getInstructionsInfo();

        expect(map.size).toBe(2);
        expect(map.has('a.instructions.md')).toBe(true);
        expect(map.has('b.instructions.md')).toBe(true);
    });
});
