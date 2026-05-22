import { vi } from 'vitest';
import type { WorkspaceContextDetector } from '../../src/workspace-context-detector';

export function createFakeDetector(): WorkspaceContextDetector {
    return {
        get: vi.fn((_key: string) => false),
        onDidDetect: vi.fn(() => ({ dispose: vi.fn() })),
    } as unknown as WorkspaceContextDetector;
}
