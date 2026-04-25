import { McpToolsCatalogEntry } from './mcp-tools-catalog-entry.js';
import type { McpToolsCatalogOptions } from './types/mcp-tools-catalog-options.js';
import type { McpToolsEntry } from './types/mcp-tools-entry.js';
import { McpToolsManifest } from './mcp-tools-manifest.js';

/**
 * The MCP-tools catalog. During the step #4 migration this class
 * accepts either:
 *   - the new `McpToolsManifest` (preferred, used by `extension.ts`), or
 *   - the legacy `(entries, options)` form that fixture-based tests use.
 *
 * The legacy overload will be removed once tests have been migrated to
 * build a `McpToolsManifest` directly.
 */
export class McpToolsCatalog {
    private readonly entries: readonly McpToolsCatalogEntry[];
    private readonly descriptions?: ReadonlyMap<string, string>;

    constructor(manifest: McpToolsManifest);
    constructor(data: readonly McpToolsEntry[], options?: McpToolsCatalogOptions);
    constructor(
        arg: McpToolsManifest | readonly McpToolsEntry[],
        options: McpToolsCatalogOptions = {},
    ) {
        if (arg instanceof McpToolsManifest) {
            const projection = McpToolsCatalog.#projectFromManifest(arg);
            this.entries = projection.entries.map(d => new McpToolsCatalogEntry(d, projection.descriptions.get(d.key)));
            this.descriptions = projection.descriptions;
            return;
        }

        const data = arg;
        this.entries = data.map(d => new McpToolsCatalogEntry(d, options.descriptions?.get(d.key)));
        this.descriptions = options.descriptions;
    }

    get all(): readonly McpToolsCatalogEntry[] {
        return this.entries;
    }

    get count(): number {
        return this.entries.length;
    }

    getDescription(toolOrTaskName: string): string | undefined {
        return this.descriptions?.get(toolOrTaskName);
    }

    /**
     * Project a `McpToolsManifest` back to the legacy flat-entry shape
     * the rest of the catalog still works with. Will be inlined and
     * removed during step #4b/c when consumers walk the manifest tree
     * directly.
     */
    static #projectFromManifest(manifest: McpToolsManifest): {
        entries: McpToolsEntry[];
        descriptions: ReadonlyMap<string, string>;
    } {
        const entries: McpToolsEntry[] = [];
        const descriptions = new Map<string, string>();

        // Iterate tools grouped by manifest's declared category order so the
        // resulting flat entries preserve top-/sub-category declaration order.
        const orderedTools = manifest.tools.slice().sort((a, b) => {
            const aTopIndex = manifest.topCategories.indexOf(a.firstCategory);
            const bTopIndex = manifest.topCategories.indexOf(b.firstCategory);
            if (aTopIndex !== bTopIndex) { return aTopIndex - bTopIndex; }
            const aSubIndex = manifest.subCategories.indexOf(a.lastCategory);
            const bSubIndex = manifest.subCategories.indexOf(b.lastCategory);
            return aSubIndex - bSubIndex;
        });

        for (const tool of orderedTools) {
            if (tool.description !== undefined) {
                descriptions.set(tool.name, tool.description);
            }

            if (tool.tasks.length === 0) {
                entries.push({
                    key: tool.name,
                    toolName: undefined,
                    label: tool.name,
                    leafCategory: tool.lastCategory,
                    workerCategory: tool.firstCategory,
                    workspaceFlags: tool.workspaceFlags.length > 0 ? tool.workspaceFlags : undefined,
                });
                continue;
            }

            for (const task of tool.tasks) {
                if (task.description !== undefined) {
                    descriptions.set(task.name, task.description);
                }
                entries.push({
                    key: task.name,
                    toolName: tool.name,
                    label: task.name,
                    leafCategory: tool.lastCategory,
                    workerCategory: tool.firstCategory,
                    workspaceFlags: tool.workspaceFlags.length > 0 ? tool.workspaceFlags : undefined,
                });
            }
        }

        return { entries, descriptions };
    }
}
