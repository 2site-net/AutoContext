import { describe, it, expect, vi } from 'vitest';
import { TreeViewStateResolver } from '#src/tree-view-state-resolver';
import { TreeViewNodeState } from '#src/tree-view-node-state';
import { AutoContextConfig } from '#src/autocontext-config';
import { McpToolEntry } from '#src/mcp-tool-entry';
import { McpCategoryEntry } from '#src/mcp-category-entry';
import { createFakeDetector } from '#testing/fakes';
import { makeInstructionsFileEntry } from '#testing/fixtures';

function makeTool(name: string, activationFlags: readonly string[], taskNames: readonly string[] = []): McpToolEntry {
    const category = new McpCategoryEntry('cat', undefined, 'worker', activationFlags);
    return new McpToolEntry(name, undefined, [category], taskNames.map(n => ({ name: n })));
}

describe('TreeViewStateResolver.resolve (instructions)', () => {
    it('returns NotDetected when activation flags are present and none match', () => {
        const detector = createFakeDetector();
        const resolver = new TreeViewStateResolver(detector);
        const entry = makeInstructionsFileEntry('lang-csharp.instructions.md', 'C#', ['Languages'], ['hasCSharpProject']);

        expect(resolver.resolve(entry, new AutoContextConfig())).toBe(TreeViewNodeState.NotDetected);
    });

    it('returns Disabled when the instructions entry has enabled:false', () => {
        const detector = createFakeDetector();
        const resolver = new TreeViewStateResolver(detector);
        const entry = makeInstructionsFileEntry('lang-csharp.instructions.md', 'C#', ['Languages']);
        const config = new AutoContextConfig({
            instructions: { 'lang-csharp.instructions.md': { enabled: false } },
        });

        expect(resolver.resolve(entry, config)).toBe(TreeViewNodeState.Disabled);
    });

    it('returns Overridden when the runtime context key is in the override set', () => {
        const detector = createFakeDetector();
        const resolver = new TreeViewStateResolver(detector);
        const entry = makeInstructionsFileEntry('lang-csharp.instructions.md', 'C#', ['Languages']);
        const overrides = new Set([entry.runtimeInfo.contextKey]);

        expect(resolver.resolve(entry, new AutoContextConfig(), overrides)).toBe(TreeViewNodeState.Overridden);
    });

    it('returns Enabled when the entry has no flags, no disable, no override', () => {
        const detector = createFakeDetector();
        const resolver = new TreeViewStateResolver(detector);
        const entry = makeInstructionsFileEntry('lang-csharp.instructions.md', 'C#', ['Languages']);

        expect(resolver.resolve(entry, new AutoContextConfig())).toBe(TreeViewNodeState.Enabled);
    });

    it('returns Enabled when at least one activation flag is detected', () => {
        const detector = createFakeDetector();
        vi.mocked(detector.get).mockImplementation((k: string) => k === 'hasCSharpProject');
        const resolver = new TreeViewStateResolver(detector);
        const entry = makeInstructionsFileEntry('lang-csharp.instructions.md', 'C#', ['Languages'], ['hasCSharpProject']);

        expect(resolver.resolve(entry, new AutoContextConfig())).toBe(TreeViewNodeState.Enabled);
    });
});

describe('TreeViewStateResolver.resolveTask', () => {
    it('returns NotDetected when the parent tool has activation flags and none match', () => {
        const detector = createFakeDetector();
        const resolver = new TreeViewStateResolver(detector);
        const tool = makeTool('analyze_csharp_code', ['hasCSharpProject'], ['scan']);

        expect(resolver.resolveTask(tool, tool.tasks[0], new AutoContextConfig())).toBe(TreeViewNodeState.NotDetected);
    });

    it('returns Disabled when the task is listed in disabledTasks', () => {
        const detector = createFakeDetector();
        const resolver = new TreeViewStateResolver(detector);
        const tool = makeTool('analyze_csharp_code', [], ['scan']);
        const config = new AutoContextConfig({
            mcpTools: { analyze_csharp_code: { disabledTasks: ['scan'] } },
        });

        expect(resolver.resolveTask(tool, tool.tasks[0], config)).toBe(TreeViewNodeState.Disabled);
    });

    it('returns Enabled when activation matches and the task is not disabled', () => {
        const detector = createFakeDetector();
        const resolver = new TreeViewStateResolver(detector);
        const tool = makeTool('analyze_csharp_code', [], ['scan']);

        expect(resolver.resolveTask(tool, tool.tasks[0], new AutoContextConfig())).toBe(TreeViewNodeState.Enabled);
    });

    it('returns Enabled even when the parent tool itself is disabled (tasks are independent)', () => {
        const detector = createFakeDetector();
        const resolver = new TreeViewStateResolver(detector);
        const tool = makeTool('analyze_csharp_code', [], ['scan']);
        const config = new AutoContextConfig({
            mcpTools: { analyze_csharp_code: false },
        });

        expect(resolver.resolveTask(tool, tool.tasks[0], config)).toBe(TreeViewNodeState.Enabled);
    });
});
