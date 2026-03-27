import { describe, it, expect } from 'vitest';
import { servers } from '../../src/server-entry';

describe('servers catalog', () => {
    it('should have unique scopes', () => {
        const scopes = servers.map(s => s.scope);

        expect(new Set(scopes).size).toBe(scopes.length);
    });

    it('should have a contextKey on workspace-specific server entries', () => {
        const dotnet = servers.find(s => s.scope === 'dotnet')!;
        const git = servers.find(s => s.scope === 'git')!;

        expect(dotnet.contextKey).toBe('hasDotnet');
        expect(git.contextKey).toBe('hasGit');
    });

    it('should not require a contextKey on the editorconfig server', () => {
        const editorconfig = servers.find(s => s.scope === 'editorconfig')!;

        expect(editorconfig.contextKey).toBeUndefined();
    });
});
