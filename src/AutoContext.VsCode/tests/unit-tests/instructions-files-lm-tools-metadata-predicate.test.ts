import { describe, it, expect, vi, beforeEach } from 'vitest';
import { InstructionsFilesLmToolsMetadataPredicate } from '#src/instructions-files-lm-tools-metadata-predicate';
import { createFakeApplyToMatcher } from '#testing/fakes';
import type { InstructionsFilesLmToolsMetadataView } from '#types/instructions-files-lm-tools-metadata-view.js';

const csharpView: InstructionsFilesLmToolsMetadataView = {
    name: 'lang-csharp.instructions.md',
    key: 'lang-csharp',
    fileName: 'lang-csharp.instructions.md',
    description: 'C# code style and naming',
    version: '1.0.0',
    applyTo: '**/*.cs',
    hasChangelog: true,
    categories: ['Languages', 'Backend'],
    sections: [
        { heading: 'Security', anchor: 'security' },
        { heading: 'Naming', anchor: 'naming' },
        { heading: 'Casing', anchor: 'naming-casing', parent: 'Naming' },
    ],
};

const tsView: InstructionsFilesLmToolsMetadataView = {
    name: 'lang-typescript.instructions.md',
    key: 'lang-typescript',
    fileName: 'lang-typescript.instructions.md',
    description: 'TypeScript style guide',
    version: '0.9.0',
    applyTo: '**/*.ts',
    hasChangelog: false,
    categories: ['Languages', 'Frontend'],
    sections: [
        { heading: 'Imports', anchor: 'imports' },
    ],
};

const designView: InstructionsFilesLmToolsMetadataView = {
    name: 'design.instructions.md',
    key: 'design',
    fileName: 'design.instructions.md',
    description: 'Design principles',
    version: '2.0.0',
    hasChangelog: false,
    categories: ['General'],
    sections: [],
};

const allViews: readonly InstructionsFilesLmToolsMetadataView[] = [csharpView, tsView, designView];

beforeEach(() => {
    vi.clearAllMocks();
});

describe('InstructionsFilesLmToolsMetadataPredicate.evaluate', () => {
    it('should return the full input when the predicate is empty', async () => {
        const predicate = new InstructionsFilesLmToolsMetadataPredicate(createFakeApplyToMatcher());

        const result = await predicate.evaluate({}, allViews);

        expect(result.kind).toBe('ok');
        if (result.kind !== 'ok') return;
        expect(result.results.map(r => r.view.name)).toEqual(allViews.map(v => v.name));
    });

    it('should match string fields via case-insensitive regex', async () => {
        const predicate = new InstructionsFilesLmToolsMetadataPredicate(createFakeApplyToMatcher());

        const result = await predicate.evaluate({ description: 'CODE STYLE' }, allViews);

        expect(result.kind).toBe('ok');
        if (result.kind !== 'ok') return;
        expect(result.results.map(r => r.view.name)).toEqual(['lang-csharp.instructions.md']);
    });

    it('should match boolean fields via exact equality', async () => {
        const predicate = new InstructionsFilesLmToolsMetadataPredicate(createFakeApplyToMatcher());

        const result = await predicate.evaluate({ hasChangelog: true }, allViews);

        expect(result.kind).toBe('ok');
        if (result.kind !== 'ok') return;
        expect(result.results.map(r => r.view.name)).toEqual(['lang-csharp.instructions.md']);
    });

    it('should match sections.level via numeric exact equality and report matchedAnchors', async () => {
        const predicate = new InstructionsFilesLmToolsMetadataPredicate(createFakeApplyToMatcher());

        const result = await predicate.evaluate({ 'sections.level': 3 }, allViews);

        expect(result.kind).toBe('ok');
        if (result.kind !== 'ok') return;
        expect.soft(result.results.map(r => r.view.name)).toEqual(['lang-csharp.instructions.md']);
        expect.soft(result.results[0].matchedAnchors).toEqual(['naming-casing']);
    });

    it('should match a regex against any element of categories', async () => {
        const predicate = new InstructionsFilesLmToolsMetadataPredicate(createFakeApplyToMatcher());

        const result = await predicate.evaluate({ categories: '^Frontend$' }, allViews);

        expect(result.kind).toBe('ok');
        if (result.kind !== 'ok') return;
        expect(result.results.map(r => r.view.name)).toEqual(['lang-typescript.instructions.md']);
    });

    it('should AND multiple predicate keys', async () => {
        const predicate = new InstructionsFilesLmToolsMetadataPredicate(createFakeApplyToMatcher());

        const result = await predicate.evaluate(
            { description: 'style', hasChangelog: false },
            allViews,
        );

        expect(result.kind).toBe('ok');
        if (result.kind !== 'ok') return;
        expect(result.results.map(r => r.view.name)).toEqual(['lang-typescript.instructions.md']);
    });

    it('should intersect sections.* clauses inside a single section and return its anchor', async () => {
        const predicate = new InstructionsFilesLmToolsMetadataPredicate(createFakeApplyToMatcher());

        const result = await predicate.evaluate(
            { 'sections.heading': 'Casing', 'sections.parent': 'Naming' },
            allViews,
        );

        expect(result.kind).toBe('ok');
        if (result.kind !== 'ok') return;
        expect.soft(result.results.map(r => r.view.name)).toEqual(['lang-csharp.instructions.md']);
        expect.soft(result.results[0].matchedAnchors).toEqual(['naming-casing']);
    });

    it('should drop a view when no single section satisfies all sections.* clauses', async () => {
        const predicate = new InstructionsFilesLmToolsMetadataPredicate(createFakeApplyToMatcher());

        // The 'Security' section has no parent; the AND with sections.parent fails everywhere.
        const result = await predicate.evaluate(
            { 'sections.heading': 'Security', 'sections.parent': 'Naming' },
            allViews,
        );

        expect(result.kind).toBe('ok');
        if (result.kind !== 'ok') return;
        expect(result.results).toEqual([]);
    });

    it('should dispatch applyTo to the matcher as a glob, not a regex', async () => {
        const matcher = createFakeApplyToMatcher();
        vi.mocked(matcher.matches).mockImplementation(async (_input, instructionApplyTo) =>
            instructionApplyTo === '**/*.cs',
        );
        const predicate = new InstructionsFilesLmToolsMetadataPredicate(matcher);

        const result = await predicate.evaluate({ applyTo: 'src/**/*.cs' }, allViews);

        expect(result.kind).toBe('ok');
        if (result.kind !== 'ok') return;
        expect.soft(result.results.map(r => r.view.name)).toEqual(['lang-csharp.instructions.md']);
        expect.soft(matcher.matches).toHaveBeenCalledWith('src/**/*.cs', '**/*.cs');
    });

    it('should drop views without applyTo when an applyTo clause is supplied', async () => {
        const predicate = new InstructionsFilesLmToolsMetadataPredicate(createFakeApplyToMatcher());

        const result = await predicate.evaluate({ applyTo: 'src/**/*' }, allViews);

        expect(result.kind).toBe('ok');
        if (result.kind !== 'ok') return;
        expect(result.results.map(r => r.view.name)).not.toContain('design.instructions.md');
    });

    it('should return unknown-field for an unrecognised predicate key', async () => {
        const predicate = new InstructionsFilesLmToolsMetadataPredicate(createFakeApplyToMatcher());

        const result = await predicate.evaluate({ bogus: 'x' }, allViews);

        expect(result).toMatchObject({ kind: 'error', error: 'unknown-field', field: 'bogus' });
    });

    it('should return type-mismatch when the value type does not match the field kind', async () => {
        const predicate = new InstructionsFilesLmToolsMetadataPredicate(createFakeApplyToMatcher());

        const result = await predicate.evaluate({ hasChangelog: 'true' }, allViews);

        expect(result).toMatchObject({ kind: 'error', error: 'type-mismatch', field: 'hasChangelog' });
    });

    it('should return invalid-regex for a malformed pattern', async () => {
        const predicate = new InstructionsFilesLmToolsMetadataPredicate(createFakeApplyToMatcher());

        const result = await predicate.evaluate({ description: '(' }, allViews);

        expect(result).toMatchObject({ kind: 'error', error: 'invalid-regex', field: 'description' });
    });

    it('should return pattern-too-long when a pattern exceeds 256 characters', async () => {
        const predicate = new InstructionsFilesLmToolsMetadataPredicate(createFakeApplyToMatcher());
        const longPattern = 'a'.repeat(257);

        const result = await predicate.evaluate({ description: longPattern }, allViews);

        expect(result).toMatchObject({ kind: 'error', error: 'pattern-too-long', field: 'description' });
    });
});
