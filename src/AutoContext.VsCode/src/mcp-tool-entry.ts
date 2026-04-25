import { McpItemEntry } from './mcp-item-entry.js';
import type { McpCategoryEntry } from './mcp-category-entry.js';
import { McpToolRuntimeInfo } from './mcp-tool-runtime-info.js';
import { McpTaskEntry } from './mcp-task-entry.js';

export interface McpTaskInit {
    readonly name: string;
    readonly description?: string;
}

/**
 * A tool from `mcp-tools-manifest.json`. Tools belong to one or more
 * categories — the first is the top-level (worker) category, the last
 * is the leaf category used to group it in the tree. Tasks (if any)
 * are constructed eagerly in the constructor so they can hold a
 * back-reference to their parent tool.
 */
export class McpToolEntry extends McpItemEntry {
    readonly #runtimeInfo: McpToolRuntimeInfo;
    readonly #tasks: readonly McpTaskEntry[];
    #activationFlags?: readonly string[];

    constructor(
        name: string,
        description: string | undefined,
        readonly categories: readonly McpCategoryEntry[],
        tasks: readonly McpTaskInit[],
    ) {
        super(name, description);
        this.#runtimeInfo = new McpToolRuntimeInfo(name);
        this.#tasks = tasks.map(t => new McpTaskEntry(t.name, t.description, this));
    }

    get runtimeInfo(): McpToolRuntimeInfo {
        return this.#runtimeInfo;
    }

    get tasks(): readonly McpTaskEntry[] {
        return this.#tasks;
    }

    get firstCategory(): McpCategoryEntry {
        return this.categories[0];
    }

    get lastCategory(): McpCategoryEntry {
        return this.categories[this.categories.length - 1];
    }

    /** Union of activation flags from every category this tool belongs to. The tool is shown when ANY of these workspace flags is detected (logical OR); empty means always-on. */
    get activationFlags(): readonly string[] {
        return this.#activationFlags ??=
            [...new Set(this.categories.flatMap(c => c.activationFlags))];
    }
}
