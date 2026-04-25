import { McpItemEntry } from './mcp-item-entry.js';

/**
 * A category from `mcp-tools-manifest.json`. A category is *top-level*
 * when it has a `workerId` (it backs an MCP server / worker process);
 * categories without a `workerId` are sub-categories that group tools
 * under a top-level category in the tree view.
 */
export class McpCategoryEntry extends McpItemEntry {
    constructor(
        name: string,
        description: string | undefined,
        readonly workerId: string | undefined,
        readonly when: readonly string[],
    ) {
        super(name, description);
    }

    get isTopLevel(): boolean {
        return this.workerId !== undefined;
    }
}
