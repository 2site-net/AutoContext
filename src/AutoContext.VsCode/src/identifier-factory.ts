import { randomUUID } from 'node:crypto';

/**
 * Centralised producer of the per-window identifier and the named-pipe
 * "service addresses" used across the extension. Pipe addresses follow
 * the canonical shape `autocontext.<role>#<instance-id>` where:
 *
 * - `<role>` names a logical service (`log`, `health-monitor`,
 *   `worker-control`, `worker-dotnet`, `worker-workspace`, `worker-web`).
 * - `<instance-id>` is the 12-hex-character per-window identifier
 *   produced by {@link IdentifierFactory.createInstanceId} and shared
 *   by every process in one VS Code window so two windows can run
 *   side-by-side without colliding on pipe names.
 *
 * `#` is intentional and shell-safe mid-token in cmd / pwsh / bash
 * (the comment behaviour only triggers at the start of a token or
 * after whitespace).
 */
export class IdentifierFactory {
    /**
     * Returns a fresh 12-character lowercase-hex per-window identifier
     * (e.g. `abc123def456`). Generated once at extension activation
     * and threaded to every server and child process via the
     * `--instance-id` switch.
     */
    static createInstanceId(): string {
        return randomUUID().replace(/-/g, '').slice(0, 12);
    }

    /**
     * Formats the canonical Windows named-pipe address for a service
     * hosted in this extension instance:
     * `autocontext.<role>#<instance-id>`.
     *
     * @param role        Service role (e.g. `log`, `health-monitor`,
     *                    `worker-control`, `worker-dotnet`).
     * @param instanceId  Per-window id from {@link createInstanceId}.
     */
    static createServiceAddress(role: string, instanceId: string): string {
        if (!role || !role.trim()) {
            throw new Error('Service role must be a non-empty string.');
        }
        if (!instanceId || !instanceId.trim()) {
            throw new Error('Instance id must be a non-empty string.');
        }
        return `autocontext.${role}#${instanceId}`;
    }
}
