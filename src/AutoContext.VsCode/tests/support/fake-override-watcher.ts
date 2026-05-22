import { vi } from 'vitest';
import type { InstructionsFilesOverrideWatcher } from '../../src/instructions-files-override-watcher';

export function createFakeOverrideWatcher(): InstructionsFilesOverrideWatcher {
    return {
        onDidChange: vi.fn(() => ({ dispose: vi.fn() })),
        watch: vi.fn(async () => {}),
        isOverridden: vi.fn((_fileName: string) => false),
        getOverrideVersion: vi.fn((_fileName: string) => undefined as string | undefined),
        getOverriddenFileNames: vi.fn(() => new Set<string>()),
        dispose: vi.fn(),
    } as unknown as InstructionsFilesOverrideWatcher;
}
