import { describe, it, expect, vi, beforeEach } from 'vitest';
import { InstructionsFilesLmToolsContentSearch } from '#src/instructions-files-lm-tools-content-search';
import { createFakeConfigManager } from '#support/fake-config-manager';
import { createFakeContentProjector } from '#support/fake-content-projector';
import { createFakeDetector } from '#support/fake-detector';
import { createFakeLogger } from '#support/fake-logger';
import { createFakeOverrideWatcher } from '#support/fake-override-watcher';
import { makeInstructionsFileEntry, makeInstructionsFilesManifest } from '#support/make-entry';
import type { InstructionsFileMetadata } from '#types/instructions-file-metadata.js';
import type { InstructionsFileProjection } from '#types/instructions-file-projection.js';
import type { InstructionsFileSectionWithOffsets } from '#types/instructions-file-section-with-offsets.js';

interface Fixture {
    readonly name: string;
    readonly description: string;
    readonly body: string;
    readonly sections: readonly InstructionsFileSectionWithOffsets[];
}

const detector = createFakeDetector();
const configManager = createFakeConfigManager();
const overrideWatcher = createFakeOverrideWatcher();

beforeEach(() => {
    vi.clearAllMocks();
});

function section(
    heading: string, anchor: string, charStart: number, charEnd: number, parent?: string,
): InstructionsFileSectionWithOffsets {
    return parent
        ? { heading, anchor, parent, charStart, charEnd }
        : { heading, anchor, charStart, charEnd };
}

function makeSearch(fixtures: readonly Fixture[]) {
    const runtimeContext = { detector, configManager, overrideWatcher };
    const entries = fixtures.map(f => makeInstructionsFileEntry(
        f.name, f.name, ['Test'], undefined,
        { description: f.description, version: '1.0.0' } satisfies InstructionsFileMetadata,
        runtimeContext,
    ));
    const manifest = makeInstructionsFilesManifest(entries);

    const projector = createFakeContentProjector();
    const projections = new Map<string, InstructionsFileProjection>(
        fixtures.map(f => [f.name, { body: f.body, sections: f.sections }]),
    );
    vi.mocked(projector.project).mockImplementation(async name => projections.get(name));

    const overrideListeners: Array<() => void> = [];
    vi.mocked(overrideWatcher.onDidChange).mockImplementation((listener: () => void) => {
        overrideListeners.push(listener);
        return { dispose: vi.fn() };
    });

    const search = new InstructionsFilesLmToolsContentSearch(
        manifest, projector, overrideWatcher, createFakeLogger(),
    );
    return {
        search,
        projector,
        fireOverride: () => { for (const l of overrideListeners) l(); },
    };
}

describe('InstructionsFilesLmToolsContentSearch.search', () => {
    describe('tokenizer', () => {
        it('should match a "configure await" query against a body containing "ConfigureAwait"', async () => {
            const { search } = makeSearch([{
                name: 'a.instructions.md', description: '',
                body: '## Tip\nPrefer ConfigureAwait everywhere.\n',
                sections: [section('Tip', 'tip', 0, 41)],
            }]);

            const hits = await search.search('configure await');

            expect(hits.map(h => h.name)).toEqual(['a.instructions.md']);
        });

        it('should match a "ConfigureAwait" query only against a body containing the joined form', async () => {
            const { search } = makeSearch([
                {
                    name: 'a.instructions.md', description: '',
                    body: '## Tip\nUse ConfigureAwait everywhere.\n',
                    sections: [section('Tip', 'tip', 0, 38)],
                },
                {
                    name: 'b.instructions.md', description: '',
                    body: '## Tip\nUse configure await pattern.\n',
                    sections: [section('Tip', 'tip', 0, 36)],
                },
            ]);

            const hits = await search.search('ConfigureAwait');

            expect(hits.map(h => h.name)).toEqual(['a.instructions.md']);
        });

        it('should split on kebab boundaries: "async await" matches "dotnet-async-await"', async () => {
            const { search } = makeSearch([{
                name: 'a.instructions.md', description: '',
                body: '## Topic\nThe dotnet-async-await guide.\n',
                sections: [section('Topic', 'topic', 0, 39)],
            }]);

            const hits = await search.search('async await');

            expect(hits.map(h => h.name)).toEqual(['a.instructions.md']);
        });

        it('should match "nuget" against a description containing "NuGet"', async () => {
            const { search } = makeSearch([{
                name: 'a.instructions.md', description: 'NuGet packaging',
                body: '## Topic\nUse the package manager to install packages.\n',
                sections: [section('Topic', 'topic', 0, 55)],
            }]);

            const hits = await search.search('nuget');

            expect(hits.map(h => h.name)).toEqual(['a.instructions.md']);
        });
    });

    describe('AND semantics', () => {
        it('should drop files missing any distinct query token', async () => {
            const { search } = makeSearch([
                {
                    name: 'a.instructions.md', description: '',
                    body: '## S\nalpha alpha alpha.\n',
                    sections: [section('S', 's', 0, 24)],
                },
                {
                    name: 'b.instructions.md', description: '',
                    body: '## S\nalpha and beta together.\n',
                    sections: [section('S', 's', 0, 30)],
                },
            ]);

            const hits = await search.search('alpha beta');

            expect(hits.map(h => h.name)).toEqual(['b.instructions.md']);
        });
    });

    describe('ranking', () => {
        it('should weight description hits 2× and break score ties by name ascending', async () => {
            const { search } = makeSearch([
                {
                    name: 'b-desc.instructions.md', description: 'alpha guide',
                    body: '## S\nbody.\n',
                    sections: [section('S', 's', 0, 12)],
                },
                {
                    name: 'a-body.instructions.md', description: '',
                    body: '## S\nalpha alpha mention.\n',
                    sections: [section('S', 's', 0, 26)],
                },
                {
                    name: 'c-body.instructions.md', description: '',
                    body: '## S\nalpha alpha mention.\n',
                    sections: [section('S', 's', 0, 26)],
                },
            ]);

            const hits = await search.search('alpha');

            // b-desc: descHits=1 → 2; a-body & c-body: contentHits=2 → 2 each.
            // Tie-break by name asc: a-body, b-desc, c-body.
            expect.soft(hits.map(h => h.name)).toEqual([
                'a-body.instructions.md',
                'b-desc.instructions.md',
                'c-body.instructions.md',
            ]);
            expect.soft(hits.every(h => h.score === 2)).toBe(true);
        });
    });

    describe('excerpts', () => {
        it('should attach section attribution and sectionLevel from the offsets index', async () => {
            // Pad the top section so its excerpt window doesn't overlap
            // with the sub-section (excerptRadius = 80; overlapping
            // windows are deduped by the engine).
            const filler = 'lorem '.repeat(40);
            const top = `## Top\nfoo here. ${filler}`;
            const sub = `### Sub\nfoo there.\n`;
            const body = top + sub;
            const { search } = makeSearch([{
                name: 'a.instructions.md', description: '',
                body,
                sections: [
                    section('Top', 'top', 0, top.length),
                    section('Sub', 'top-sub', top.length, body.length, 'Top'),
                ],
            }]);

            const hits = await search.search('foo');

            expect(hits).toHaveLength(1);
            const excerpts = hits[0].excerpts;
            expect.soft(excerpts).toHaveLength(2);
            expect.soft(excerpts[0].section).toBe('Top');
            expect.soft(excerpts[0].sectionLevel).toBe(2);
            expect.soft(excerpts[0].anchor).toBe('top');
            expect.soft(excerpts[1].section).toBe('Sub');
            expect.soft(excerpts[1].sectionLevel).toBe(3);
            expect.soft(excerpts[1].anchor).toBe('top-sub');
        });
    });

    describe('override invalidation', () => {
        it('should rebuild the index after the override watcher fires', async () => {
            const { search, projector, fireOverride } = makeSearch([{
                name: 'a.instructions.md', description: '',
                body: '## S\nalpha.\n',
                sections: [section('S', 's', 0, 12)],
            }]);

            await search.search('alpha');
            await search.search('alpha');
            expect.soft(projector.project).toHaveBeenCalledTimes(1);

            fireOverride();
            await search.search('alpha');

            expect.soft(projector.project).toHaveBeenCalledTimes(2);
        });
    });
});
