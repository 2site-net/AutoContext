import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { generateInstructionsFilesMetadata } from '#src/instructions-files-metadata-generator';
import type { InstructionsFilesMetadata } from '#types/instructions-files-metadata.js';

interface CuratedFile {
    readonly name: string;
}

function curatedManifest(files: readonly CuratedFile[]): string {
    return JSON.stringify(
        {
            schemaVersion: '1',
            categories: [],
            instructions: files.map(f => ({ label: f.name, name: f.name, categories: [] })),
        },
        null,
        2,
    );
}

function frontmatter(opts: { key: string; description?: string; applyTo?: string; version?: string }): string {
    const lines = ['---', `name: "${opts.key} (v${opts.version ?? '1.0.0'})"`];
    if (opts.description !== undefined) {
        lines.push(`description: "${opts.description}"`);
    }
    if (opts.applyTo !== undefined) {
        lines.push(`applyTo: "${opts.applyTo}"`);
    }
    lines.push('---', '');
    return lines.join('\n');
}

describe('generateInstructionsFilesMetadata', () => {
    let root: string;
    let instructionsDir: string;
    let resourcesDir: string;

    beforeEach(() => {
        root = mkdtempSync(join(tmpdir(), 'autocontext-instructions-files-metadata-generator-'));
        instructionsDir = join(root, 'instructions');
        resourcesDir = join(root, 'resources');
        mkdirSync(instructionsDir, { recursive: true });
        mkdirSync(resourcesDir, { recursive: true });
    });

    afterEach(() => {
        rmSync(root, { recursive: true, force: true });
    });

    function writeInstruction(fileName: string, content: string): void {
        writeFileSync(join(instructionsDir, fileName), content);
    }

    function writeCurated(files: readonly CuratedFile[]): void {
        writeFileSync(join(resourcesDir, 'instructions-files.json'), curatedManifest(files));
    }

    describe('headings', () => {
        it('should extract a single ## section', () => {
            writeInstruction(
                'flat.instructions.md',
                frontmatter({ key: 'flat', description: 'd' }) + '## Only\n\nbody\n',
            );
            writeCurated([{ name: 'flat.instructions.md' }]);

            const result = generateInstructionsFilesMetadata(root);

            const sections = result.instructions[0].sections;
            expect(sections).toHaveLength(1);
            expect(sections[0]).toMatchObject({ heading: 'Only', anchor: 'only' });
            expect(sections[0].parent).toBeUndefined();
            expect(sections[0]).not.toHaveProperty('charStart');
            expect(sections[0]).not.toHaveProperty('charEnd');
            expect(sections[0]).not.toHaveProperty('level');
        });

        it('should attribute ### sections to nearest preceding ## as parent', () => {
            writeInstruction(
                'grouped.instructions.md',
                frontmatter({ key: 'grouped', description: 'd' }) +
                    '## Naming\n\n### Types\n\nbody\n### Members\n\nbody\n## Other\n',
            );
            writeCurated([{ name: 'grouped.instructions.md' }]);

            const result = generateInstructionsFilesMetadata(root);

            const sections = result.instructions[0].sections;
            expect(sections.map(s => [s.heading, s.parent, s.anchor])).toEqual([
                ['Naming', undefined, 'naming'],
                ['Types', 'Naming', 'naming-types'],
                ['Members', 'Naming', 'naming-members'],
                ['Other', undefined, 'other'],
            ]);
        });

        it('should ignore #### and deeper headings', () => {
            writeInstruction(
                'deep.instructions.md',
                frontmatter({ key: 'deep', description: 'd' }) +
                    '## Top\n\n#### Skipped\n\n##### Skipped Too\n\n## After\n',
            );
            writeCurated([{ name: 'deep.instructions.md' }]);

            const result = generateInstructionsFilesMetadata(root);

            expect(result.instructions[0].sections.map(s => s.heading)).toEqual(['Top', 'After']);
        });

        it('should ignore headings inside fenced code blocks', () => {
            writeInstruction(
                'fence.instructions.md',
                frontmatter({ key: 'fence', description: 'd' }) +
                    '## Real\n\n```md\n## Not A Heading\n```\n\n## After\n',
            );
            writeCurated([{ name: 'fence.instructions.md' }]);

            const result = generateInstructionsFilesMetadata(root);

            expect(result.instructions[0].sections.map(s => s.heading)).toEqual(['Real', 'After']);
        });

        it('should reject duplicate anchors caused by heading collisions', () => {
            writeInstruction(
                'dup.instructions.md',
                frontmatter({ key: 'dup', description: 'd' }) + '## Same\n\n## Same\n',
            );
            writeCurated([{ name: 'dup.instructions.md' }]);

            expect(() => generateInstructionsFilesMetadata(root)).toThrow(/duplicate section anchor 'same'/);
        });
    });

    describe('frontmatter validation', () => {
        it('should fail when name is missing', () => {
            writeInstruction(
                'noname.instructions.md',
                '---\ndescription: "d"\n---\n',
            );
            writeCurated([{ name: 'noname.instructions.md' }]);

            expect(() => generateInstructionsFilesMetadata(root)).toThrow(/missing required `name`/);
        });

        it('should fail when name shape is invalid', () => {
            writeInstruction(
                'bad.instructions.md',
                '---\nname: "Bad Name"\ndescription: "d"\n---\n',
            );
            writeCurated([{ name: 'bad.instructions.md' }]);

            expect(() => generateInstructionsFilesMetadata(root)).toThrow(/does not match `<key> \(vX\.Y\.Z\)`/);
        });

        it('should fail when key portion of name does not match file basename', () => {
            writeInstruction(
                'foo.instructions.md',
                frontmatter({ key: 'bar', description: 'd' }),
            );
            writeCurated([{ name: 'foo.instructions.md' }]);

            expect(() => generateInstructionsFilesMetadata(root)).toThrow(/key 'bar' does not equal file basename 'foo'/);
        });

        it('should fail when description is missing or empty', () => {
            writeInstruction(
                'nodesc.instructions.md',
                '---\nname: "nodesc (v1.0.0)"\ndescription: ""\n---\n',
            );
            writeCurated([{ name: 'nodesc.instructions.md' }]);

            expect(() => generateInstructionsFilesMetadata(root)).toThrow(/missing or empty `description`/);
        });

        it('should fail when applyTo is present but empty', () => {
            writeInstruction(
                'empty-apply.instructions.md',
                '---\nname: "empty-apply (v1.0.0)"\ndescription: "d"\napplyTo: ""\n---\n',
            );
            writeCurated([{ name: 'empty-apply.instructions.md' }]);

            expect(() => generateInstructionsFilesMetadata(root)).toThrow(/`applyTo` is present but empty/);
        });

        it('should omit applyTo from output when absent in source', () => {
            writeInstruction(
                'no-apply.instructions.md',
                frontmatter({ key: 'no-apply', description: 'd' }),
            );
            writeCurated([{ name: 'no-apply.instructions.md' }]);

            const entry = generateInstructionsFilesMetadata(root).instructions[0];
            expect(entry).not.toHaveProperty('applyTo');
        });

        it('should preserve applyTo in output when present', () => {
            writeInstruction(
                'with-apply.instructions.md',
                frontmatter({ key: 'with-apply', description: 'd', applyTo: '**/*.ts' }),
            );
            writeCurated([{ name: 'with-apply.instructions.md' }]);

            expect(generateInstructionsFilesMetadata(root).instructions[0].applyTo).toBe('**/*.ts');
        });
    });

    describe('cross-validation against curated manifest', () => {
        it('should fail when an instruction file is missing from the curated manifest', () => {
            writeInstruction('orphan.instructions.md', frontmatter({ key: 'orphan', description: 'd' }));
            writeCurated([]);

            expect(() => generateInstructionsFilesMetadata(root)).toThrow(/orphan\.instructions\.md/);
        });

        it('should fail when the curated manifest references a missing file', () => {
            writeInstruction('present.instructions.md', frontmatter({ key: 'present', description: 'd' }));
            writeCurated([{ name: 'present.instructions.md' }, { name: 'missing.instructions.md' }]);

            expect(() => generateInstructionsFilesMetadata(root)).toThrow(/missing\.instructions\.md/);
        });

        it('should exempt copilot.instructions.md from the curated manifest', () => {
            writeInstruction('copilot.instructions.md', frontmatter({ key: 'copilot', description: 'd' }));
            writeCurated([]);

            const result = generateInstructionsFilesMetadata(root);
            expect(result.instructions.map(i => i.key)).toEqual(['copilot']);
        });

        it('should exempt autocontext.instructions.md from the curated manifest', () => {
            writeInstruction('autocontext.instructions.md', frontmatter({ key: 'autocontext', description: 'd' }));
            writeCurated([]);

            const result = generateInstructionsFilesMetadata(root);
            expect(result.instructions.map(i => i.key)).toEqual(['autocontext']);
        });
    });

    describe('output shape', () => {
        beforeEach(() => {
            writeInstruction('zebra.instructions.md', frontmatter({ key: 'zebra', description: 'z' }) + '## Z\n');
            writeInstruction('alpha.instructions.md', frontmatter({ key: 'alpha', description: 'a' }) + '## A\n');
            writeCurated([{ name: 'zebra.instructions.md' }, { name: 'alpha.instructions.md' }]);
        });

        it('should emit schemaVersion "1"', () => {
            expect(generateInstructionsFilesMetadata(root).schemaVersion).toBe('1');
        });

        it('should sort entries by key ascending', () => {
            const keys = generateInstructionsFilesMetadata(root).instructions.map(i => i.key);
            expect(keys).toEqual(['alpha', 'zebra']);
        });

        it('should extract version from the name suffix', () => {
            const map = mapByKey(generateInstructionsFilesMetadata(root));
            expect(map.get('alpha')!.version).toBe('1.0.0');
        });

        it('should produce a sha256-prefixed contentHash', () => {
            const entry = generateInstructionsFilesMetadata(root).instructions[0];
            expect(entry.contentHash).toMatch(/^sha256:[0-9a-f]{64}$/);
        });

        it('should report hasChangelog=true when a sibling .CHANGELOG.md exists', () => {
            writeFileSync(join(instructionsDir, 'alpha.CHANGELOG.md'), '# changes\n');

            const map = mapByKey(generateInstructionsFilesMetadata(root));
            expect(map.get('alpha')!.hasChangelog).toBe(true);
            expect(map.get('zebra')!.hasChangelog).toBe(false);
        });

        it('should produce deterministic output across runs', () => {
            const a = JSON.stringify(generateInstructionsFilesMetadata(root));
            const b = JSON.stringify(generateInstructionsFilesMetadata(root));
            expect(a).toBe(b);
        });
    });

    function mapByKey(meta: InstructionsFilesMetadata): Map<string, InstructionsFilesMetadata['instructions'][number]> {
        return new Map(meta.instructions.map(i => [i.key, i]));
    }

    it('should integrate with the real extension root', () => {
        const extRoot = join(__dirname, '..', '..');
        const meta = generateInstructionsFilesMetadata(extRoot);
        // Sanity check the result against the live committed inputs.
        expect(meta.schemaVersion).toBe('1');
        expect(meta.instructions.length).toBeGreaterThan(0);
        // copilot is always present.
        expect(meta.instructions.some(i => i.key === 'copilot')).toBe(true);
        // Body of code-review is consumed; it has no applyTo.
        const codeReview = meta.instructions.find(i => i.key === 'code-review');
        expect(codeReview).toBeDefined();
        expect(codeReview!.applyTo).toBeUndefined();
        // Sections carry only catalog fields; no offsets or level in the JSON.
        expect(codeReview!.sections.length).toBeGreaterThan(0);
        for (const section of codeReview!.sections) {
            expect(section).not.toHaveProperty('charStart');
            expect(section).not.toHaveProperty('charEnd');
            expect(section).not.toHaveProperty('level');
            expect(typeof section.heading).toBe('string');
            expect(typeof section.anchor).toBe('string');
        }
    });
});
