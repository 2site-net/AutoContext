/**
 * Construction-time payload for an `McpTaskEntry` — the subset of
 * task fields read from `resources/mcp-tools.json` before the entry
 * is wired up with its parent tool and runtime context.
 */
export interface McpTaskInit {
    readonly name: string;
    readonly description?: string;
}
