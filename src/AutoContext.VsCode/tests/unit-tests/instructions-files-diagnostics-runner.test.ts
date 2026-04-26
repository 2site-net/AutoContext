import { describe, it, expect, vi, beforeEach } from 'vitest';
import { InstructionsFilesDiagnosticsRunner } from '../../src/instructions-files-diagnostics-runner';
import { InstructionsFileParser } from '../../src/instructions-file-parser';
import { makeInstructionsFileEntry, makeInstructionsFilesManifest } from './_fixtures/make-entry';
import type { AutoContextConfig } from '../../src/types/autocontext-config';
import type { AutoContextConfigManager } from '../../src/autocontext-config';
import type { InstructionsFilesManifest } from '../../src/instructions-files-manifest';
import type { InstructionsFileParsedResult } from '../../src/types/instructions-file-parsed-result';
import type { InstructionsFileParsedCachedResult } from '../../src/types/instructions-file-parsed-cached-result';

vi.mock('../../src/instructions-file-parser', () => ({
    InstructionsFileParser: { fromFile: vi.fn() },
}));

const fromFile = vi.mocked(InstructionsFileParser.fromFile);

function makeRunner(config: AutoContextConfig, manifest: InstructionsFilesManifest): InstructionsFilesDiagnosticsRunner {
    const configManager = { read: async () => config } as unknown as AutoContextConfigManager;
    return new InstructionsFilesDiagnosticsRunner('/ext', configManager, manifest);
}

function parsedResult(diagnostics: InstructionsFileParsedResult['diagnostics']): InstructionsFileParsedCachedResult {
    return {
        content: '',
        result: {
            instructions: [],
            diagnostics,
            frontmatter: {},
        },
    };
}

beforeEach(() => {
    vi.clearAllMocks();
});

describe('InstructionsFilesDiagnosticsRunner.collect', () => {
    it('should return an empty list when the manifest has no entries', async () => {
        const runner = makeRunner({}, makeInstructionsFilesManifest([]));

        const records = await runner.collect();

        expect(records).toEqual([]);
        expect(fromFile).not.toHaveBeenCalled();
    });

    it('should produce a parse-error record when the parser throws', async () => {
        fromFile.mockRejectedValueOnce(new Error('boom'));
        const runner = makeRunner({}, makeInstructionsFilesManifest([
            makeInstructionsFileEntry('a.instructions.md', 'A', ['cat']),
        ]));

        const records = await runner.collect();

        expect(records).toEqual([
            { kind: 'parse-error', entry: 'a.instructions.md', message: 'boom' },
        ]);
    });

    it('should stringify non-Error throwables in parse-error records', async () => {
        fromFile.mockRejectedValueOnce('weird');
        const runner = makeRunner({}, makeInstructionsFilesManifest([
            makeInstructionsFileEntry('a.instructions.md', 'A', ['cat']),
        ]));

        const records = await runner.collect();

        expect(records[0]?.message).toBe('weird');
    });

    it('should suppress missing-id diagnostics when warnOnMissingId is unset', async () => {
        fromFile.mockResolvedValueOnce(parsedResult([
            { kind: 'missing-id', line: 0, message: 'no id' },
            { kind: 'duplicate-id', line: 4, message: 'dup' },
        ]));
        const runner = makeRunner({}, makeInstructionsFilesManifest([
            makeInstructionsFileEntry('a.instructions.md', 'A', ['cat']),
        ]));

        const records = await runner.collect();

        expect(records).toEqual([
            { kind: 'duplicate-id', entry: 'a.instructions.md', line: 4, message: 'dup' },
        ]);
    });

    it('should include missing-id diagnostics when warnOnMissingId is true', async () => {
        fromFile.mockResolvedValueOnce(parsedResult([
            { kind: 'missing-id', line: 7, message: 'no id' },
        ]));
        const runner = makeRunner({ diagnostic: { warnOnMissingId: true } }, makeInstructionsFilesManifest([
            makeInstructionsFileEntry('a.instructions.md', 'A', ['cat']),
        ]));

        const records = await runner.collect();

        expect(records).toEqual([
            { kind: 'missing-id', entry: 'a.instructions.md', line: 7, message: 'no id' },
        ]);
    });

    it('should flatten records from all entries in manifest order', async () => {
        fromFile.mockImplementation(async (path: string) => {
            if (path.endsWith('a.instructions.md')) {
                return parsedResult([{ kind: 'duplicate-id', line: 1, message: 'a-dup' }]);
            }
            if (path.endsWith('b.instructions.md')) {
                return parsedResult([{ kind: 'malformed-id', line: 2, message: 'b-bad' }]);
            }
            return parsedResult([]);
        });
        const runner = makeRunner({}, makeInstructionsFilesManifest([
            makeInstructionsFileEntry('a.instructions.md', 'A', ['cat']),
            makeInstructionsFileEntry('b.instructions.md', 'B', ['cat']),
        ]));

        const records = await runner.collect();

        expect(records).toEqual([
            { kind: 'duplicate-id', entry: 'a.instructions.md', line: 1, message: 'a-dup' },
            { kind: 'malformed-id', entry: 'b.instructions.md', line: 2, message: 'b-bad' },
        ]);
    });

    it('should parse files concurrently rather than sequentially', async () => {
        const inFlight: number[] = [];
        let active = 0;
        fromFile.mockImplementation(async () => {
            active++;
            inFlight.push(active);
            await new Promise(resolve => setTimeout(resolve, 5));
            active--;
            return parsedResult([]);
        });
        const runner = makeRunner({}, makeInstructionsFilesManifest([
            makeInstructionsFileEntry('a.instructions.md', 'A', ['cat']),
            makeInstructionsFileEntry('b.instructions.md', 'B', ['cat']),
            makeInstructionsFileEntry('c.instructions.md', 'C', ['cat']),
        ]));

        await runner.collect();

        expect(Math.max(...inFlight)).toBeGreaterThan(1);
    });
});
