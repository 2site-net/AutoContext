import { describe, it, expect } from 'vitest';
import { mcpServers } from '../../src/ui-constants';

describe('servers catalog', () => {
    it('should have unique scopes', () => {
        const scopes = mcpServers.map(s => s.scope);

        expect.soft(new Set(scopes).size).toBe(scopes.length);
    });

    it('should have a contextKey on workspace-specific server entries', () => {
        const dotnet = mcpServers.find(s => s.scope === 'dotnet')!;
        const git = mcpServers.find(s => s.scope === 'git')!;

        expect(dotnet.contextKey).toBe('hasDotNet');
        expect.soft(git.contextKey).toBe('hasGit');
    });

    it('should not require a contextKey on the editorconfig server', () => {
        const editorconfig = mcpServers.find(s => s.scope === 'editorconfig')!;

        expect.soft(editorconfig.contextKey).toBeUndefined();
    });
});
