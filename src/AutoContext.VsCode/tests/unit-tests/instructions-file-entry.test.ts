import { describe, it, expect, vi } from 'vitest';
import { TreeViewNodeState } from '#src/tree-view-node-state';
import { AutoContextConfig } from '#src/autocontext-config';
import { createFakeConfigManager } from '#support/fake-config-manager';
import { createFakeDetector } from '#support/fake-detector';
import { createFakeOverrideWatcher } from '#support/fake-override-watcher';
import { makeInstructionsFileEntry } from '#support/make-entry';

function setup(config: AutoContextConfig = new AutoContextConfig()) {
    const detector = createFakeDetector();
    const overrideWatcher = createFakeOverrideWatcher();
    const configManager = createFakeConfigManager();
    vi.mocked(configManager.readSync).mockReturnValue(config);
    return { detector, overrideWatcher, configManager, runtimeContext: { detector, overrideWatcher, configManager } };
}

describe('InstructionsFileEntry.resolveState', () => {
    it('should return NotDetected when activation flags are present and none match', () => {
        const { runtimeContext } = setup();
        const entry = makeInstructionsFileEntry(
            'lang-csharp.instructions.md', 'C#', ['Languages'], ['hasCSharpProject'], undefined, runtimeContext);

        expect(entry.resolveState()).toBe(TreeViewNodeState.NotDetected);
    });

    it('should return Disabled when the instructions entry has enabled:false', () => {
        const { runtimeContext } = setup(new AutoContextConfig({
            instructions: { 'lang-csharp.instructions.md': { enabled: false } },
        }));
        const entry = makeInstructionsFileEntry(
            'lang-csharp.instructions.md', 'C#', ['Languages'], undefined, undefined, runtimeContext);

        expect(entry.resolveState()).toBe(TreeViewNodeState.Disabled);
    });

    it('should return Overridden when the runtime context key is in the override set', () => {
        const { runtimeContext, overrideWatcher } = setup();
        const entry = makeInstructionsFileEntry(
            'lang-csharp.instructions.md', 'C#', ['Languages'], undefined, undefined, runtimeContext);
        vi.mocked(overrideWatcher.isOverridden).mockImplementation((name: string) => name === 'lang-csharp.instructions.md');

        expect(entry.resolveState()).toBe(TreeViewNodeState.Overridden);
    });

    it('should return Enabled when the entry has no flags, no disable, no override', () => {
        const { runtimeContext } = setup();
        const entry = makeInstructionsFileEntry(
            'lang-csharp.instructions.md', 'C#', ['Languages'], undefined, undefined, runtimeContext);

        expect(entry.resolveState()).toBe(TreeViewNodeState.Enabled);
    });

    it('should return Enabled when at least one activation flag is detected', () => {
        const { runtimeContext, detector } = setup();
        vi.mocked(detector.get).mockImplementation((k: string) => k === 'hasCSharpProject');
        const entry = makeInstructionsFileEntry(
            'lang-csharp.instructions.md', 'C#', ['Languages'], ['hasCSharpProject'], undefined, runtimeContext);

        expect(entry.resolveState()).toBe(TreeViewNodeState.Enabled);
    });
});
