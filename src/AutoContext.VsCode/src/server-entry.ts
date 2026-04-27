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
     * Used as a short label in log output and output-channel names.
     */
    getShortName(): string {
        return ServerEntry.stripNamePrefix(this.name);
    }

    static stripNamePrefix(fullName: string): string {
        return fullName.replace(/^AutoContext\./, '');
    }
}
