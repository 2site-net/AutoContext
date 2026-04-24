/**
 * Composes the transport endpoint (today: a Windows named-pipe name) from
 * a worker's short id, optionally appending a per-window isolation suffix.
 *
 * Mirrors the C# `EndpointFormatter.Format` + `EndpointOptions.Resolve`
 * pair in `AutoContext.Mcp.Server/Workers/Transport/` so every process in
 * one window — workers spawned by the extension and `Mcp.Server` spawned
 * by VS Code — agrees on the exact pipe names to open and connect to.
 */

const PipeBaseName = 'autocontext.worker';

/**
 * Returns the endpoint (named-pipe name) for a worker.
 *
 * - Base shape: `autocontext.worker-<id>` (e.g. `id = "dotnet"` →
 *   `autocontext.worker-dotnet`).
 * - With a non-empty `suffix`: `autocontext.worker-<id>-<suffix>` —
 *   enables multiple VS Code windows (or smoke tests) to coexist
 *   without pipe collisions on the same machine.
 *
 * @param id      Worker identifier — kebab-case lowercase (e.g. `workspace`,
 *                `dotnet`, `web`). Must be non-empty.
 * @param suffix  Optional per-window isolation suffix. Empty/whitespace is
 *                treated as absent.
 */
export function formatEndpoint(id: string, suffix?: string): string {
    if (!id || !id.trim()) {
        throw new Error('Worker id must be a non-empty string.');
    }

    const normalizedSuffix = suffix?.trim();
    const base = `${PipeBaseName}-${id}`;

    return normalizedSuffix ? `${base}-${normalizedSuffix}` : base;
}
