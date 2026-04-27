import { vi } from 'vitest';
import type { HealthMonitorServer } from '../../../src/health-monitor-server';

export function createFakeHealthMonitor(
    overrides: Partial<Record<'isRunning', (key: string) => boolean>> = {},
): HealthMonitorServer {
    return {
        getPipeName: vi.fn(() => 'autocontext-health-abc123'),
        isRunning: vi.fn(overrides.isRunning ?? (() => false)),
        onDidChange: vi.fn(() => ({ dispose: vi.fn() })),
    } as unknown as HealthMonitorServer;
}
