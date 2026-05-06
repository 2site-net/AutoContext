import { describe, it, expect, vi } from 'vitest';
import { TreeViewNodeState } from '#src/tree-view-node-state';
import { AutoContextConfig } from '#src/autocontext-config';
import { McpToolEntry } from '#src/mcp-tool-entry';
import { McpCategoryEntry } from '#src/mcp-category-entry';
import { createFakeDetector, createFakeConfigManager } from '#testing/fakes';
import type { WorkspaceContextDetector } from '#src/workspace-context-detector';
import type { AutoContextConfigManager } from '#src/autocontext-config-manager';

function makeTool(
    name: string,
    activationFlags: readonly string[],
    taskNames: readonly string[],
    detector: WorkspaceContextDetector,
    configManager: AutoContextConfigManager,
): McpToolEntry {
    const category = new McpCategoryEntry('cat', undefined, 'worker', activationFlags);
    return new McpToolEntry(name, undefined, [category], taskNames.map(n => ({ name: n })), { detector, configManager });
}

function setup(config: AutoContextConfig = new AutoContextConfig()) {
    const detector = createFakeDetector();
    const configManager = createFakeConfigManager();
    vi.mocked(configManager.readSync).mockReturnValue(config);
    return { detector, configManager };
}

describe('McpTaskEntry.resolveState', () => {
    it('returns NotDetected when the parent tool has activation flags and none match', () => {
        const { detector, configManager } = setup();
        const tool = makeTool('analyze_csharp_code', ['hasCSharpProject'], ['scan'], detector, configManager);

        expect(tool.tasks[0].resolveState()).toBe(TreeViewNodeState.NotDetected);
    });

    it('returns Disabled when the task is listed in disabledTasks', () => {
        const { detector, configManager } = setup(new AutoContextConfig({
            mcpTools: { analyze_csharp_code: { disabledTasks: ['scan'] } },
        }));
        const tool = makeTool('analyze_csharp_code', [], ['scan'], detector, configManager);

        expect(tool.tasks[0].resolveState()).toBe(TreeViewNodeState.Disabled);
    });

    it('returns Enabled when activation matches and the task is not disabled', () => {
        const { detector, configManager } = setup();
        const tool = makeTool('analyze_csharp_code', [], ['scan'], detector, configManager);

        expect(tool.tasks[0].resolveState()).toBe(TreeViewNodeState.Enabled);
    });

    it('returns Enabled even when the parent tool itself is disabled (tasks are independent)', () => {
        const { detector, configManager } = setup(new AutoContextConfig({
            mcpTools: { analyze_csharp_code: false },
        }));
        const tool = makeTool('analyze_csharp_code', [], ['scan'], detector, configManager);

        expect(tool.tasks[0].resolveState()).toBe(TreeViewNodeState.Enabled);
    });
});
