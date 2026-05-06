import { describe, it, expect, vi } from 'vitest';
import { TreeViewNodeState } from '#src/tree-view-node-state';
import { AutoContextConfig } from '#src/autocontext-config';
import { createFakeDetector, createFakeConfigManager } from '#testing/fakes';
import { makeInstructionsFileEntry } from '#testing/fixtures';

function setup(config: AutoContextConfig = new AutoContextConfig()) {
    const detector = createFakeDetector();
    const configManager = createFakeConfigManager();
    vi.mocked(configManager.readSync).mockReturnValue(config);
    return { detector, configManager };
}

describe('InstructionsFileEntry.resolveState', () => {
    it('returns NotDetected when activation flags are present and none match', () => {
        const { detector, configManager } = setup();
        const entry = makeInstructionsFileEntry(
            'lang-csharp.instructions.md', 'C#', ['Languages'], ['hasCSharpProject'], undefined, detector, configManager);

        expect(entry.resolveState()).toBe(TreeViewNodeState.NotDetected);
    });

    it('returns Disabled when the instructions entry has enabled:false', () => {
        const { detector, configManager } = setup(new AutoContextConfig({
            instructions: { 'lang-csharp.instructions.md': { enabled: false } },
        }));
        const entry = makeInstructionsFileEntry(
            'lang-csharp.instructions.md', 'C#', ['Languages'], undefined, undefined, detector, configManager);

        expect(entry.resolveState()).toBe(TreeViewNodeState.Disabled);
    });

    it('returns Overridden when the runtime context key is in the override set', () => {
        const { detector, configManager } = setup();
        const entry = makeInstructionsFileEntry(
            'lang-csharp.instructions.md', 'C#', ['Languages'], undefined, undefined, detector, configManager);
        vi.mocked(detector.getOverriddenContextKeys).mockReturnValue(new Set([entry.runtimeInfo.contextKey]));

        expect(entry.resolveState()).toBe(TreeViewNodeState.Overridden);
    });

    it('returns Enabled when the entry has no flags, no disable, no override', () => {
        const { detector, configManager } = setup();
        const entry = makeInstructionsFileEntry(
            'lang-csharp.instructions.md', 'C#', ['Languages'], undefined, undefined, detector, configManager);

        expect(entry.resolveState()).toBe(TreeViewNodeState.Enabled);
    });

    it('returns Enabled when at least one activation flag is detected', () => {
        const { detector, configManager } = setup();
        vi.mocked(detector.get).mockImplementation((k: string) => k === 'hasCSharpProject');
        const entry = makeInstructionsFileEntry(
            'lang-csharp.instructions.md', 'C#', ['Languages'], ['hasCSharpProject'], undefined, detector, configManager);

        expect(entry.resolveState()).toBe(TreeViewNodeState.Enabled);
    });
});
