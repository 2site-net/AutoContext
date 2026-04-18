import { vi } from 'vitest';
import type { HealthMonitorServer } from '../../../src/health-monitor';

export function createFakeHealthMonitor(
    overrides: Partial<Record<'isServerHealthy' | 'isServerPartiallyHealthy', (g: string) => boolean>> = {},
): HealthMonitorServer {
    return {
        getPipeName: vi.fn(() => 'autocontext-health-abc123'),
        isServerHealthy: vi.fn(overrides.isServerHealthy ?? (() => false)),
        isServerPartiallyHealthy: vi.fn(overrides.isServerPartiallyHealthy ?? (() => false)),
        onDidChange: vi.fn(() => ({ dispose: vi.fn() })),
    } as unknown as HealthMonitorServer;
}
