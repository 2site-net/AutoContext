import { describe, it, expect } from 'vitest';
import { McpServersRegistry } from '../../src/mcp-servers-registry';

describe('servers catalog', () => {
    it('should have unique categories', () => {
        const categories = McpServersRegistry.all.map(s => s.category);

        expect.soft(new Set(categories).size).toBe(categories.length);
    });

    it('should have a contextKey on workspace-specific server entries', () => {
        const dotnet = McpServersRegistry.all.find(s => s.category === 'dotnet')!;
        const git = McpServersRegistry.all.find(s => s.category === 'git')!;

        expect(dotnet.contextKey).toBe('hasDotNet');
        expect.soft(git.contextKey).toBe('hasGit');
    });

    it('should not require a contextKey on the editorconfig server', () => {
        const editorconfig = McpServersRegistry.all.find(s => s.category === 'editorconfig')!;

        expect.soft(editorconfig.contextKey).toBeUndefined();
    });
});
