import { describe, it, expect, vi, beforeEach } from 'vitest';
import { InstructionsFilesLmToolsListHandler } from '#src/instructions-files-lm-tools-list-handler';
import { InstructionsFilesLmToolsMetadataPredicate } from '#src/instructions-files-lm-tools-metadata-predicate';
import { InstructionsFilesLmToolsMetadataViews } from '#src/instructions-files-lm-tools-metadata-views';
import { InstructionsFilesLmToolsSearchByMetadataHandler } from '#src/instructions-files-lm-tools-search-by-metadata-handler';
import {
    createFakeApplyToMatcher,
    createFakeConfigManager,
    createFakeDetector,
    createFakeOverrideWatcher,
} from '#testing/fakes';
import { makeInstructionsFileEntry, makeInstructionsFilesManifest } from '#testing/fixtures';
import type { InstructionsFileMetadata } from '#types/instructions-file-metadata.js';

const csharpMeta: InstructionsFileMetadata = {
    description: 'C# style', version: '1.0.0', applyTo: '**/*.cs',
    hasChangelog: true, sections: [{ heading: 'Naming', anchor: 'naming' }],
};
const tsMeta: InstructionsFileMetadata = {
    description: 'TypeScript style', version: '0.9.0', applyTo: '**/*.ts',
    hasChangelog: false, sections: [],
};
const designMeta: InstructionsFileMetadata = {
    description: 'Design', version: '2.0.0', hasChangelog: false, sections: [],
};

const configManager = createFakeConfigManager();
const detector = createFakeDetector();
const overrideWatcher = createFakeOverrideWatcher();

beforeEach(() => {
    vi.clearAllMocks();
});

function makeHandlers(metadataOverrides?: ReadonlyMap<string, InstructionsFileMetadata>): {
    readonly list: InstructionsFilesLmToolsListHandler;
    readonly byMetadata: InstructionsFilesLmToolsSearchByMetadataHandler;
} {
    const runtimeContext = { detector, overrideWatcher, configManager };
    const meta = metadataOverrides ?? new Map<string, InstructionsFileMetadata>([
        ['lang-csharp.instructions.md', csharpMeta],
        ['lang-typescript.instructions.md', tsMeta],
        ['design.instructions.md', designMeta],
    ]);
    const entries = [...meta].map(([name, m]) =>
        makeInstructionsFileEntry(name, name, [name === 'design.instructions.md' ? 'General' : 'Languages'],
            undefined, m, runtimeContext),
    );
    const manifest = makeInstructionsFilesManifest(entries);
    const views = new InstructionsFilesLmToolsMetadataViews(manifest, meta);
    const matcher = createFakeApplyToMatcher();
    // Realistic stand-in: equality on the applyTo glob.
    vi.mocked(matcher.matches).mockImplementation(async (input, applyTo) => input === applyTo);
    const predicate = new InstructionsFilesLmToolsMetadataPredicate(matcher);
    const byMetadata = new InstructionsFilesLmToolsSearchByMetadataHandler(manifest, views, predicate);
    const list = new InstructionsFilesLmToolsListHandler(byMetadata);
    return { list, byMetadata };
}

describe('InstructionsFilesLmToolsListHandler.handle', () => {
    it('list({}) is equivalent to byMetadata({})', async () => {
        const { list, byMetadata } = makeHandlers();

        const a = await list.handle({});
        const b = await byMetadata.handle({});

        expect(a).toEqual(b);
    });

    it('list({ applyTo }) is equivalent to byMetadata({ predicate: { applyTo } })', async () => {
        const { list, byMetadata } = makeHandlers();

        const a = await list.handle({ applyTo: '**/*.cs' });
        const b = await byMetadata.handle({ predicate: { applyTo: '**/*.cs' } });

        expect.soft(a).toEqual(b);
        if (a.kind !== 'ok') return;
        expect.soft(a.results.map(r => r.name)).toEqual(['lang-csharp.instructions.md']);
    });

    it('list({ category }) translates to a regex-anchored equality clause on categories', async () => {
        const { list, byMetadata } = makeHandlers();

        const a = await list.handle({ category: 'Languages' });
        const b = await byMetadata.handle({ predicate: { categories: '^Languages$' } });

        expect.soft(a).toEqual(b);
        if (a.kind !== 'ok') return;
        expect.soft(a.results.map(r => r.name)).toEqual([
            'lang-csharp.instructions.md',
            'lang-typescript.instructions.md',
        ]);
    });

    it('escapes regex metacharacters in the category name', async () => {
        const trickyMeta: InstructionsFileMetadata = {
            description: 'Tricky', version: '1.0.0', hasChangelog: false, sections: [],
        };
        const runtimeContext = { detector, overrideWatcher, configManager };
        const entries = [
            makeInstructionsFileEntry('foo.instructions.md', 'Foo', ['C++ (native)'], undefined, trickyMeta, runtimeContext),
            makeInstructionsFileEntry('bar.instructions.md', 'Bar', ['Languages'], undefined, trickyMeta, runtimeContext),
        ];
        const manifest = makeInstructionsFilesManifest(entries);
        const meta = new Map([
            ['foo.instructions.md', trickyMeta],
            ['bar.instructions.md', trickyMeta],
        ]);
        const views = new InstructionsFilesLmToolsMetadataViews(manifest, meta);
        const predicate = new InstructionsFilesLmToolsMetadataPredicate(createFakeApplyToMatcher());
        const byMetadata = new InstructionsFilesLmToolsSearchByMetadataHandler(manifest, views, predicate);
        const list = new InstructionsFilesLmToolsListHandler(byMetadata);

        const result = await list.handle({ category: 'C++ (native)' });

        expect(result.kind).toBe('ok');
        if (result.kind !== 'ok') return;
        expect(result.results.map(r => r.name)).toEqual(['foo.instructions.md']);
    });

    it('forwards includeSections to the underlying handler', async () => {
        const { list, byMetadata } = makeHandlers();

        const a = await list.handle({ includeSections: true });
        const b = await byMetadata.handle({ includeSections: true });

        expect.soft(a).toEqual(b);
        if (a.kind !== 'ok') return;
        const csharp = a.results.find(r => r.name === 'lang-csharp.instructions.md');
        expect.soft(csharp?.sections).toEqual([{ heading: 'Naming', anchor: 'naming' }]);
    });

    it('combines applyTo and category into the equivalent compound predicate', async () => {
        const { list, byMetadata } = makeHandlers();

        const a = await list.handle({ applyTo: '**/*.cs', category: 'Languages' });
        const b = await byMetadata.handle({
            predicate: { applyTo: '**/*.cs', categories: '^Languages$' },
        });

        expect(a).toEqual(b);
    });
});
