import { vi } from 'vitest';
import type { WorkspaceContextDetector } from '../../../src/workspace-context-detector';

export function createFakeDetector(): WorkspaceContextDetector {
    return {
        get: vi.fn((_key: string) => false),
        getOverriddenContextKeys: vi.fn(() => new Set<string>()),
        getOverrideVersion: vi.fn((_fileName: string) => undefined as string | undefined),
        onDidDetect: vi.fn(() => ({ dispose: vi.fn() })),
    } as unknown as WorkspaceContextDetector;
}
