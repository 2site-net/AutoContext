import { describe, it, expect, vi, beforeEach } from 'vitest';
import { InstructionsFilesLmToolsGetHandler } from '#src/instructions-files-lm-tools-get-handler';
import { AutoContextConfig } from '#src/autocontext-config';
import {
    createFakeConfigManager,
    createFakeContentProjector,
    createFakeDetector,
    createFakeOverrideWatcher,
} from '#testing/fakes';
import { makeInstructionsFileEntry, makeInstructionsFilesManifest } from '#testing/fixtures';
import type { InstructionsFileMetadata } from '#types/instructions-file-metadata.js';
import type { InstructionsFileSectionWithOffsets } from '#types/instructions-file-section-with-offsets.js';

const meta: InstructionsFileMetadata = {
    description: 'Sample', version: '1.0.0', hasChangelog: false, sections: [],
};

let currentConfig: AutoContextConfig = new AutoContextConfig();
const configManager = createFakeConfigManager();
const detector = createFakeDetector();
const overrideWatcher = createFakeOverrideWatcher();

beforeEach(() => {
    vi.clearAllMocks();
    currentConfig = new AutoContextConfig();
    vi.mocked(configManager.readSync).mockImplementation(() => currentConfig);
});

function section(
    heading: string, anchor: string, charStart: number, charEnd: number,
): InstructionsFileSectionWithOffsets {
    return { heading, anchor, charStart, charEnd };
}

function makeHandler(opts?: {
    readonly projection?: { body: string; sections: readonly InstructionsFileSectionWithOffsets[] } | undefined;
}) {
    const runtimeContext = { detector, configManager, overrideWatcher };
    const entries = [
        makeInstructionsFileEntry('a.instructions.md', 'a.instructions.md', ['Test'], undefined, meta, runtimeContext),
    ];
    const manifest = makeInstructionsFilesManifest(entries);
    const projector = createFakeContentProjector();
    if (opts && 'projection' in opts) {
        vi.mocked(projector.project).mockResolvedValue(opts.projection);
    } else {
        vi.mocked(projector.project).mockResolvedValue({
            body: 'whole body', sections: [section('Top', 'top', 0, 10)],
        });
    }
    return {
        handler: new InstructionsFilesLmToolsGetHandler(manifest, projector),
        projector,
    };
}

describe('InstructionsFilesLmToolsGetHandler.handle', () => {
    it('returns kind:not-found when the manifest has no matching entry', async () => {
        const { handler } = makeHandler();

        const result = await handler.handle({ name: 'missing.instructions.md' });

        expect(result).toEqual({ kind: 'not-found', name: 'missing.instructions.md' });
    });

    it('returns the disabled envelope when the entry is inactive', async () => {
        currentConfig = new AutoContextConfig({
            instructions: { 'a.instructions.md': { enabled: false } },
        });
        const { handler } = makeHandler();

        const result = await handler.handle({ name: 'a.instructions.md' });

        expect(result).toEqual({ name: 'a.instructions.md', key: 'a', disabled: true });
    });

    it('returns kind:not-found when the projector returns undefined', async () => {
        const { handler } = makeHandler({ projection: undefined });

        const result = await handler.handle({ name: 'a.instructions.md' });

        expect(result).toEqual({ kind: 'not-found', name: 'a.instructions.md' });
    });

    it('returns the whole body when sections is omitted', async () => {
        const body = '## Top\nbody one.\n## Bot\nbody two.\n';
        const { handler } = makeHandler({
            projection: {
                body,
                sections: [section('Top', 'top', 0, 17), section('Bot', 'bot', 17, body.length)],
            },
        });

        const result = await handler.handle({ name: 'a.instructions.md' });

        expect.soft(result).toMatchObject({ name: 'a.instructions.md', content: body });
        if ('returnedSections' in result) {
            expect.soft(result.returnedSections).toEqual(['top', 'bot']);
        }
    });

    it('returns the whole body when sections is an empty array', async () => {
        const body = '## Top\nbody one.\n';
        const { handler } = makeHandler({
            projection: { body, sections: [section('Top', 'top', 0, body.length)] },
        });

        const result = await handler.handle({ name: 'a.instructions.md', sections: [] });

        expect.soft(result).toMatchObject({ name: 'a.instructions.md', content: body });
        if ('returnedSections' in result) {
            expect.soft(result.returnedSections).toEqual(['top']);
        }
    });

    it('joins requested sections in document order, deduping anchors', async () => {
        const body = '## A\naaa.\n## B\nbbb.\n## C\nccc.\n';
        const aSlice = body.slice(0, 10);
        const bSlice = body.slice(10, 20);
        const cSlice = body.slice(20, body.length);
        const { handler } = makeHandler({
            projection: {
                body,
                sections: [
                    section('A', 'a', 0, 10),
                    section('B', 'b', 10, 20),
                    section('C', 'c', 20, body.length),
                ],
            },
        });

        const result = await handler.handle({
            name: 'a.instructions.md',
            sections: ['c', 'a', 'a'],
        });

        expect(result).toMatchObject({
            name: 'a.instructions.md',
            content: `${aSlice}\n${cSlice}`,
            returnedSections: ['a', 'c'],
        });
        // bSlice should not appear.
        if ('content' in result) {
            expect.soft(result.content).not.toContain(bSlice.trim());
        }
    });

    it('reports notFoundSections when some requested anchors are unknown', async () => {
        const body = '## A\naaa.\n';
        const { handler } = makeHandler({
            projection: { body, sections: [section('A', 'a', 0, body.length)] },
        });

        const result = await handler.handle({
            name: 'a.instructions.md',
            sections: ['a', 'ghost'],
        });

        expect(result).toMatchObject({
            name: 'a.instructions.md',
            returnedSections: ['a'],
            notFoundSections: ['ghost'],
        });
    });

    it('returns empty content with all-unknown anchors in notFoundSections', async () => {
        const body = '## A\naaa.\n';
        const { handler } = makeHandler({
            projection: { body, sections: [section('A', 'a', 0, body.length)] },
        });

        const result = await handler.handle({
            name: 'a.instructions.md',
            sections: ['ghost', 'phantom'],
        });

        expect(result).toMatchObject({
            name: 'a.instructions.md',
            content: '',
            returnedSections: [],
            notFoundSections: ['ghost', 'phantom'],
        });
    });
});
