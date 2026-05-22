import { describe, it, expect, vi, beforeEach } from 'vitest';
import { InstructionsFilesLmToolsSearchByContentHandler } from '#src/instructions-files-lm-tools-search-by-content-handler';
import { AutoContextConfig } from '#src/autocontext-config';
import { createFakeApplyToMatcher } from '#support/fake-apply-to-matcher';
import { createFakeConfigManager } from '#support/fake-config-manager';
import { createFakeContentSearch } from '#support/fake-content-search';
import { createFakeDetector } from '#support/fake-detector';
import { createFakeOverrideWatcher } from '#support/fake-override-watcher';
import { makeInstructionsFileEntry, makeInstructionsFilesManifest } from '#support/make-entry';
import type { InstructionsFileMetadata } from '#types/instructions-file-metadata.js';
import type { InstructionsFilesLmToolsContentMatch } from '#types/instructions-files-lm-tools-content-match.js';

const csharpMeta: InstructionsFileMetadata = {
    description: 'C# style', version: '1.0.0', applyTo: '**/*.cs', hasChangelog: false, sections: [],
};
const tsMeta: InstructionsFileMetadata = {
    description: 'TS style', version: '1.0.0', applyTo: '**/*.ts', hasChangelog: false, sections: [],
};
const designMeta: InstructionsFileMetadata = {
    description: 'Design', version: '1.0.0', hasChangelog: false, sections: [],
};

const allHits: readonly InstructionsFilesLmToolsContentMatch[] = [
    {
        name: 'lang-csharp.instructions.md', score: 5,
        excerpts: [{ text: '...', section: 'Naming', sectionLevel: 2, anchor: 'naming' }],
    },
    {
        name: 'lang-typescript.instructions.md', score: 3,
        excerpts: [{ text: '...', section: 'Imports', sectionLevel: 2, anchor: 'imports' }],
    },
    {
        name: 'design.instructions.md', score: 2,
        excerpts: [{ text: '...', section: 'Principles', sectionLevel: 2, anchor: 'principles' }],
    },
];

let currentConfig: AutoContextConfig = new AutoContextConfig();
const configManager = createFakeConfigManager();
const detector = createFakeDetector();
const overrideWatcher = createFakeOverrideWatcher();

beforeEach(() => {
    vi.clearAllMocks();
    currentConfig = new AutoContextConfig();
    vi.mocked(configManager.readSync).mockImplementation(() => currentConfig);
});

function makeHandler() {
    const runtimeContext = { detector, overrideWatcher, configManager };
    const entries = [
        makeInstructionsFileEntry('lang-csharp.instructions.md', 'C#', ['Languages'], undefined, csharpMeta, runtimeContext),
        makeInstructionsFileEntry('lang-typescript.instructions.md', 'TS', ['Languages'], undefined, tsMeta, runtimeContext),
        makeInstructionsFileEntry('design.instructions.md', 'Design', ['General'], undefined, designMeta, runtimeContext),
    ];
    const manifest = makeInstructionsFilesManifest(entries);
    const engine = createFakeContentSearch();
    const matcher = createFakeApplyToMatcher();
    const metadata = new Map<string, { applyTo?: string }>([
        ['lang-csharp.instructions.md', { applyTo: '**/*.cs' }],
        ['lang-typescript.instructions.md', { applyTo: '**/*.ts' }],
        ['design.instructions.md', {}],
    ]);
    return {
        handler: new InstructionsFilesLmToolsSearchByContentHandler(manifest, engine, matcher, metadata),
        engine,
        matcher,
    };
}

describe('InstructionsFilesLmToolsSearchByContentHandler.handle', () => {
    it('should drop disabled entries from the ranked hits', async () => {
        currentConfig = new AutoContextConfig({
            instructions: { 'lang-csharp.instructions.md': { enabled: false } },
        });
        const { handler, engine } = makeHandler();
        vi.mocked(engine.search).mockResolvedValueOnce(allHits);

        const result = await handler.handle({ query: 'foo' });

        expect(result.results.map(r => r.name)).toEqual([
            'lang-typescript.instructions.md',
            'design.instructions.md',
        ]);
    });

    it('should filter by category case-insensitively', async () => {
        const { handler, engine } = makeHandler();
        vi.mocked(engine.search).mockResolvedValueOnce(allHits);

        const result = await handler.handle({ query: 'foo', category: 'languages' });

        expect(result.results.map(r => r.name)).toEqual([
            'lang-csharp.instructions.md',
            'lang-typescript.instructions.md',
        ]);
    });

    it('should filter by applyTo via the matcher', async () => {
        const { handler, engine, matcher } = makeHandler();
        vi.mocked(engine.search).mockResolvedValueOnce(allHits);
        vi.mocked(matcher.matches).mockImplementation(
            async (input, applyTo) => input === 'src/**/*.cs' && applyTo === '**/*.cs',
        );

        const result = await handler.handle({ query: 'foo', applyTo: 'src/**/*.cs' });

        expect.soft(result.results.map(r => r.name)).toEqual(['lang-csharp.instructions.md']);
        expect.soft(matcher.matches).toHaveBeenCalled();
    });

    it('should drop entries without applyTo when the caller specifies applyTo', async () => {
        const { handler, engine } = makeHandler();
        vi.mocked(engine.search).mockResolvedValueOnce(allHits);

        const result = await handler.handle({ query: 'foo', applyTo: 'anything' });

        expect(result.results.map(r => r.name)).not.toContain('design.instructions.md');
    });

    it('should preserve the engine ranking', async () => {
        const { handler, engine } = makeHandler();
        vi.mocked(engine.search).mockResolvedValueOnce(allHits);

        const result = await handler.handle({ query: 'foo' });

        expect(result.results.map(r => r.score)).toEqual([5, 3, 2]);
    });

    it('should shape hits with key, fileName, description, score, and excerpts', async () => {
        const { handler, engine } = makeHandler();
        vi.mocked(engine.search).mockResolvedValueOnce(allHits);

        const result = await handler.handle({ query: 'foo' });

        const csharp = result.results.find(r => r.name === 'lang-csharp.instructions.md');
        expect.soft(csharp).toMatchObject({
            name: 'lang-csharp.instructions.md',
            key: 'lang-csharp',
            fileName: 'lang-csharp.instructions.md',
            description: 'C# style',
            score: 5,
        });
        expect.soft(csharp?.excerpts).toEqual([
            { text: '...', section: 'Naming', sectionLevel: 2, anchor: 'naming' },
        ]);
    });
});
