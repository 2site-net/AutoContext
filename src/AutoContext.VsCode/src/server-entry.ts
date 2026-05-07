import { NameFormatter } from './name-formatter.js';

/**
 * One entry from `resources/servers.json`. Identifies a spawnable
 * AutoContext process — its short id (`workspace`, `dotnet`, `web`,
 * `mcp-server`), the binary name (`AutoContext.Worker.DotNet`, etc.)
 * and the runtime kind so the spawn site knows whether to invoke
 * `node` or the native executable.
 */
export class ServerEntry {
    constructor(
        readonly id: string,
        readonly name: string,
        readonly type: 'dotnet' | 'node',
    ) {}

    /**
     * The name without the `AutoContext.` package prefix
     * (e.g. `"AutoContext.Worker.DotNet"` → `"Worker.DotNet"`).
     * Used as a short label in log output and as the worker
     * identity in the worker-control protocol.
     */
    getShortName(): string {
        return NameFormatter.toShortName(this.name);
    }

    /**
     * The user-facing display label
     * (e.g. `"AutoContext.Worker.DotNet"` → `"AutoContext: Worker.DotNet"`).
     * Used as the canonical output-channel name for the worker.
     */
    getDisplayName(): string {
        return NameFormatter.toDisplayName(this.name);
    }
}
