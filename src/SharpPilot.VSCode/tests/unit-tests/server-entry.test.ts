import { describe, it, expect } from 'vitest';
import { servers } from '../../src/server-entry';

describe('servers catalog', () => {
    it('should have unique categories', () => {
        const categories = servers.map(s => s.category);

        expect(new Set(categories).size).toBe(categories.length);
    });

    it('should have a contextKey on workspace-specific server entries', () => {
        const dotnet = servers.find(s => s.category === 'dotnet')!;
        const git = servers.find(s => s.category === 'git')!;

        expect(dotnet.contextKey).toBe('hasDotNet');
        expect(git.contextKey).toBe('hasGit');
    });

    it('should not require a contextKey on the editorconfig server', () => {
        const editorconfig = servers.find(s => s.category === 'editorconfig')!;

        expect(editorconfig.contextKey).toBeUndefined();
    });
});
