/**
 * Wire-shape of the snapshot pushed by `AutoContextConfigServer` to
 * subscribers. Keys are MCP Tool names from the registry; values in
 * `disabledTasks` are MCP Task names belonging to that tool.
 *
 * - `disabledTools` lists every tool whose entry resolves to disabled
 *   (either shorthand `false` or `{ enabled: false }`).
 * - `disabledTasks` lists per-tool task names recorded in
 *   `disabledTasks` on the `.autocontext.json` entry. A tool may
 *   appear in `disabledTasks` regardless of whether it appears in
 *   `disabledTools` — the parent tool's enabled state and its
 *   per-task disable list are independent (mirrors
 *   `AutoContextConfig.isToolEnabled`).
 */
export interface McpToolsDisabledSnapshot {
    readonly disabledTools: readonly string[];
    readonly disabledTasks: Readonly<Record<string, readonly string[]>>;
}
