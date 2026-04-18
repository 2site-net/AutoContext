import { vi } from 'vitest';
import type { WorkspaceServerManager } from '../../../src/workspace-server-manager';

export function createFakeWorkspaceServer(): WorkspaceServerManager {
    return {
        getPipeName: vi.fn(() => 'autocontext-workspace-abc123'),
    } as unknown as WorkspaceServerManager;
}
