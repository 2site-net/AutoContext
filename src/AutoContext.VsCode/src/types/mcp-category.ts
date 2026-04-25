/**
 * A category from `mcp-tools-manifest.json`. Categories are the manifest's
 * grouping mechanism. A category with a `workerId` is a *worker* category
 * (the top level in the tree); one without is a *leaf* category (the
 * second level under a worker).
 */
export interface McpCategory {
    /** Display name. Unique within the manifest. */
    readonly name: string;
    /**
     * Stable identifier of the worker that backs this category, when this
     * is a worker category. Absent for leaf categories.
     */
    readonly workerId?: string;
    /** Workspace-detection flags required for entries under this category. */
    readonly when?: readonly string[];
    /** Description text from the manifest. */
    readonly description?: string;
}
