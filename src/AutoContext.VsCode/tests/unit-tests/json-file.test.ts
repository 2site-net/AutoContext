import { describe, it, expect, vi, beforeEach } from 'vitest';

const { readFileSyncMock } = vi.hoisted(() => ({ readFileSyncMock: vi.fn<typeof import('node:fs').readFileSync>() }));
vi.mock('node:fs', async () => {
    const actual = await vi.importActual<typeof import('node:fs')>('node:fs');
    return { ...actual, readFileSync: readFileSyncMock };
});

const { JsonFile } = await import('../../src/json-file');

beforeEach(() => {
    readFileSyncMock.mockReset();
});

describe('JsonFile.fromUtf8', () => {
    it('returns the parsed value on success', () => {
        readFileSyncMock.mockReturnValue('{"answer":42}');

        const result = JsonFile.fromUtf8<{ answer: number }>('/some/file.json');

        expect(result).toEqual({ answer: 42 });
    });

    it('throws a contextualised error including the file path on parse failure', () => {
        readFileSyncMock.mockReturnValue('{ not valid json');

        expect(() => JsonFile.fromUtf8('/path/to/broken.json'))
            .toThrow(/Failed to parse JSON from '\/path\/to\/broken\.json'/);
    });

    it('preserves the original parser message in the wrapped error', () => {
        readFileSyncMock.mockReturnValue('{ not valid json');

        // The native message text varies between runtimes; assert the
        // wrapper preserves *something* informative beyond just the path.
        expect(() => JsonFile.fromUtf8('/x.json')).toThrow(/Failed to parse JSON from '\/x\.json': .+/);
    });
});
