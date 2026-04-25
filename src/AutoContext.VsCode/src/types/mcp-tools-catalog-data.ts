import type { McpToolsEntry } from './mcp-tools-entry.js';

/**
 * Derived view of `mcp-tools-manifest.json` consumed by the MCP-tools
 * catalog and tree provider. All ordering, grouping, and gating data
 * comes from the manifest — there are no hard-coded UI constants.
 */
export interface McpToolsCatalogData {
    /** Flat list of catalog entries (one per task, or one per task-less tool). */
    readonly entries: readonly McpToolsEntry[];
    /** Per-tool / per-task description text used for tooltips. */
    readonly descriptions: ReadonlyMap<string, string>;
    /** Top-level category names (with `workerId`), in manifest declaration order. */
    readonly serverLabelOrder: readonly string[];
    /** Sub-category names (without `workerId`), in manifest declaration order. */
    readonly categoryOrder: readonly string[];
    /** 1:1 mapping from server label to its worker id. */
    readonly serverLabelToWorkerIdMap: ReadonlyMap<string, string>;
}
