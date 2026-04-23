/**
 * An MCP Task is a unit of work dispatched by the centralized MCP server
 * (`AutoContext.Mcp.Server`) to a worker process via the named-pipe wire
 * protocol (see `docs/architecture.md` § "Protocol & Contracts").
 *
 * One connection = one task call. The worker's `McpToolService` reads the
 * per-task wire envelope, merges any `editorconfig` slice into `data` as
 * flat `editorconfig.<key>` properties, then invokes {@link execute}.
 *
 * Implementations must be pure domain logic: no MCP knowledge, no
 * EditorConfig parsing, no composition. Failures should throw — the
 * dispatcher turns them into the uniform error envelope.
 */
export interface McpTask {
    /**
     * Snake-case task identifier matching `.mcp-tools.json`. Sent on the
     * wire as `mcpTask`. Must be unique within a worker.
     */
    readonly taskName: string;

    /**
     * Executes the task. `data` is the raw wire payload with any
     * editorconfig slice already merged in.
     */
    execute(data: Record<string, unknown>, signal: AbortSignal): Promise<unknown>;
}
