import { vi } from 'vitest';
import type { HealthMonitorServer } from '../../../src/health-monitor';

export function createFakeHealthMonitor(
    overrides: Partial<Record<'isRunningServerLabel' | 'isRunning', (key: string) => boolean>> = {},
): HealthMonitorServer {
    return {
        getPipeName: vi.fn(() => 'autocontext-health-abc123'),
        isRunningServerLabel: vi.fn(overrides.isRunningServerLabel ?? (() => false)),
        isRunning: vi.fn(overrides.isRunning ?? (() => false)),
        onDidChange: vi.fn(() => ({ dispose: vi.fn() })),
    } as unknown as HealthMonitorServer;
}
