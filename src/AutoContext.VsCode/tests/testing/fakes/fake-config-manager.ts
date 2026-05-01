import { vi } from 'vitest';
import type { AutoContextConfigManager } from '../../../src/autocontext-config-manager';
import { AutoContextConfig } from '#src/autocontext-config.js';
import type { AutoContextConfigInit } from '#types/autocontext-config-init.js';

export function createFakeConfigManager(): AutoContextConfigManager {
    return {
        readSync: vi.fn(() => new AutoContextConfig()),
        read: vi.fn(async () => new AutoContextConfig()),
        onDidChange: vi.fn(() => ({ dispose: vi.fn() })),
        setInstructionEnabled: vi.fn(async () => {}),
        setMcpToolEnabled: vi.fn(async () => {}),
        setMcpTools: vi.fn(async () => {}),
    } as unknown as AutoContextConfigManager;
}

export function createMockConfigManager(config: AutoContextConfigInit | AutoContextConfig): AutoContextConfigManager {
    const instance = config instanceof AutoContextConfig ? config : new AutoContextConfig(config);
    return {
        read: vi.fn().mockResolvedValue(instance),
        onDidChange: vi.fn(() => ({ dispose: vi.fn() })),
    } as unknown as AutoContextConfigManager;
}
