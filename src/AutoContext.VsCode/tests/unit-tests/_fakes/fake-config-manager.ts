import { vi } from 'vitest';
import type { AutoContextConfigManager } from '../../../src/autocontext-config';
import type { AutoContextConfig } from '../../../src/types/autocontext-config';

export function createFakeConfigManager(): AutoContextConfigManager {
    return {
        readSync: vi.fn(() => ({})),
        read: vi.fn(async () => ({})),
        onDidChange: vi.fn(() => ({ dispose: vi.fn() })),
        setInstructionEnabled: vi.fn(async () => {}),
        setMcpToolEnabled: vi.fn(async () => {}),
        setMcpTools: vi.fn(async () => {}),
    } as unknown as AutoContextConfigManager;
}

export function createMockConfigManager(config: AutoContextConfig): AutoContextConfigManager {
    return {
        read: vi.fn().mockResolvedValue(config),
        onDidChange: vi.fn(() => ({ dispose: vi.fn() })),
    } as unknown as AutoContextConfigManager;
}
