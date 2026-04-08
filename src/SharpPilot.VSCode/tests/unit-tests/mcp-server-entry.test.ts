import { describe, it, expect } from 'vitest';
import { mcpServers } from '../../src/ui-constants';

describe('servers catalog', () => {
    it('should have unique categories', () => {
        const categories = mcpServers.map(s => s.category);

        expect.soft(new Set(categories).size).toBe(categories.length);
    });

    it('should have a contextKey on workspace-specific server entries', () => {
        const dotnet = mcpServers.find(s => s.category === 'dotnet')!;
        const git = mcpServers.find(s => s.category === 'git')!;

        expect(dotnet.contextKey).toBe('hasDotNet');
        expect.soft(git.contextKey).toBe('hasGit');
    });

    it('should not require a contextKey on the editorconfig server', () => {
        const editorconfig = mcpServers.find(s => s.category === 'editorconfig')!;

        expect.soft(editorconfig.contextKey).toBeUndefined();
    });
});
