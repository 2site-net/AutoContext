import { describe, it, expect, vi, beforeEach } from 'vitest';
import { InstructionsFilesLmToolsMetadataPredicate } from '#src/instructions-files-lm-tools-metadata-predicate';
import { InstructionsFilesLmToolsMetadataViews } from '#src/instructions-files-lm-tools-metadata-views';
import { InstructionsFilesLmToolsSearchByMetadataHandler } from '#src/instructions-files-lm-tools-search-by-metadata-handler';
import { AutoContextConfig } from '#src/autocontext-config';
import { createFakeApplyToMatcher } from '#support/fake-apply-to-matcher';
import { createFakeConfigManager } from '#support/fake-config-manager';
import { createFakeDetector } from '#support/fake-detector';
import { createFakeOverrideWatcher } from '#support/fake-override-watcher';
import { makeInstructionsFileEntry, makeInstructionsFilesManifest } from '#support/make-entry';
import type { InstructionsFileMetadata } from '#types/instructions-file-metadata.js';

const csharpMeta: InstructionsFileMetadata = {
    description: 'C# style',
    version: '1.0.0',
    applyTo: '**/*.cs',
    hasChangelog: true,
    sections: [
        { heading: 'Security', anchor: 'security' },
        { heading: 'Naming', anchor: 'naming' },
    ],
};

const tsMeta: InstructionsFileMetadata = {
    description: 'TypeScript style',
    version: '0.9.0',
    applyTo: '**/*.ts',
    hasChangelog: false,
    sections: [{ heading: 'Imports', anchor: 'imports' }],
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

function makeHandler(): InstructionsFilesLmToolsSearchByMetadataHandler {
    const runtimeContext = { detector, overrideWatcher, configManager };
    const entries = [
        makeInstructionsFileEntry('lang-csharp.instructions.md', 'C#', ['Languages'], undefined, csharpMeta, runtimeContext),
        makeInstructionsFileEntry('lang-typescript.instructions.md', 'TypeScript', ['Languages'], undefined, tsMeta, runtimeContext),
    ];
    const manifest = makeInstructionsFilesManifest(entries);
    const metadata = new Map<string, InstructionsFileMetadata>([
        ['lang-csharp.instructions.md', csharpMeta],
        ['lang-typescript.instructions.md', tsMeta],
    ]);
    const views = new InstructionsFilesLmToolsMetadataViews(manifest, metadata);
    const predicate = new InstructionsFilesLmToolsMetadataPredicate(createFakeApplyToMatcher());
    return new InstructionsFilesLmToolsSearchByMetadataHandler(manifest, views, predicate);
}

describe('InstructionsFilesLmToolsSearchByMetadataHandler.handle', () => {
    it('should drop disabled entries from the result set', async () => {
        currentConfig = new AutoContextConfig({
            instructions: { 'lang-csharp.instructions.md': { enabled: false } },
        });

        const result = await makeHandler().handle({});

        expect(result.kind).toBe('ok');
        if (result.kind !== 'ok') return;
        expect(result.results.map(r => r.name)).toEqual(['lang-typescript.instructions.md']);
    });

    it('should forward predicate-engine errors verbatim', async () => {
        const result = await makeHandler().handle({ predicate: { bogus: 'x' } });

        expect(result).toMatchObject({ kind: 'error', error: 'unknown-field', field: 'bogus' });
    });

    it('should attach matchedAnchors and sections when the predicate touches sections.*', async () => {
        const result = await makeHandler().handle({
            predicate: { 'sections.heading': 'Naming' },
        });

        expect(result.kind).toBe('ok');
        if (result.kind !== 'ok') return;
        const csharp = result.results.find(r => r.name === 'lang-csharp.instructions.md');
        expect.soft(csharp?.matchedAnchors).toEqual(['naming']);
        expect.soft(csharp?.sections).toBeDefined();
    });

    it('should omit sections when neither includeSections nor a sections.* clause is set', async () => {
        const result = await makeHandler().handle({});

        expect(result.kind).toBe('ok');
        if (result.kind !== 'ok') return;
        for (const row of result.results) {
            expect.soft(row.sections).toBeUndefined();
            expect.soft(row.matchedAnchors).toBeUndefined();
        }
    });

    it('should attach sections to every row when includeSections is true', async () => {
        const result = await makeHandler().handle({ includeSections: true });

        expect(result.kind).toBe('ok');
        if (result.kind !== 'ok') return;
        for (const row of result.results) {
            expect.soft(row.sections).toBeDefined();
        }
    });

    it('should shape catalogue rows with the manifest entry label, key, and applyTo', async () => {
        const result = await makeHandler().handle({});

        expect(result.kind).toBe('ok');
        if (result.kind !== 'ok') return;
        const csharp = result.results.find(r => r.name === 'lang-csharp.instructions.md');
        expect.soft(csharp?.label).toBe('C#');
        expect.soft(csharp?.key).toBe('lang-csharp');
        expect.soft(csharp?.fileName).toBe('lang-csharp.instructions.md');
        expect.soft(csharp?.applyTo).toBe('**/*.cs');
        expect.soft(csharp?.hasChangelog).toBe(true);
    });
});
