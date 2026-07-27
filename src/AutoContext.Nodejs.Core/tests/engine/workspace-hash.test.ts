import { describe, it, expect } from 'vitest';
import { platform } from 'node:os';
import { WORKSPACE_HASH_LENGTH, computeWorkspaceHash } from '#src/engine/workspace-hash.js';

describe('computeWorkspaceHash', () => {
    it('returns 16 uppercase hex characters', () => {
        const hash = computeWorkspaceHash(process.cwd());

        expect(hash).toHaveLength(WORKSPACE_HASH_LENGTH);
        expect(hash).toMatch(/^[0-9A-F]{16}$/);
    });

    it('is stable across repeated calls', () => {
        expect(computeWorkspaceHash(process.cwd())).toBe(computeWorkspaceHash(process.cwd()));
    });

    it('ignores trailing separators', () => {
        const base = process.cwd();

        expect(computeWorkspaceHash(`${base}/`)).toBe(computeWorkspaceHash(base));
    });

    it('distinguishes different workspaces', () => {
        const first = computeWorkspaceHash(process.cwd());
        const second = computeWorkspaceHash(`${process.cwd()}/nested`);

        expect(first).not.toBe(second);
    });

    it.runIf(platform() === 'win32')('folds case on Windows', () => {
        const lower = computeWorkspaceHash('C:\\Workspace\\Demo');
        const upper = computeWorkspaceHash('C:\\WORKSPACE\\DEMO');

        expect(lower).toBe(upper);
    });

    // Reference value produced by the engine's own hashing APIs:
    // Convert.ToHexString(SHA256.HashData(UTF8.GetBytes(@"C:\WORKSPACE\DEMO")))[..16].
    // A divergence here means a TypeScript client would dial a pipe no
    // engine ever bound.
    it.runIf(platform() === 'win32')('matches the engine reference vector', () => {
        expect(computeWorkspaceHash('C:\\Workspace\\Demo')).toBe('E8935F55006A7F3B');
    });

    // The POSIX arm of the same pin. Paths are not case-folded here, so
    // the normalised input is the resolved path verbatim.
    it.runIf(platform() !== 'win32')('matches the engine reference vector', () => {
        expect(computeWorkspaceHash('/workspace/demo')).toBe('4DE1D2F8B60DB00A');
    });

    it('rejects a blank path', () => {
        expect(() => computeWorkspaceHash('   ')).toThrow(/workspacePath/);
    });
});
