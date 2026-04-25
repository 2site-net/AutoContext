import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import type { McpToolsCatalogData } from './types/mcp-tools-catalog-data.js';
import type { McpToolsEntry } from './types/mcp-tools-entry.js';
import type { McpToolsMetadataEntry } from './types/mcp-tools-metadata-entry.js';

interface McpTaskManifest {
    name: string;
    description: string;
}

interface McpToolManifest {
    name: string;
    description: string;
    categories: readonly string[];
    tasks?: readonly McpTaskManifest[];
}

interface McpCategoryManifest {
    name: string;
    description?: string;
    when?: readonly string[];
    workerId?: string;
}

interface McpToolsManifest {
    schemaVersion?: string;
    categories: readonly McpCategoryManifest[];
    tools: readonly McpToolManifest[];
}

/**
 * Loads `mcp-tools-manifest.json` from the extension folder and projects
 * it into the shape the rest of the extension expects. The manifest is
 * the single source of truth for tool/task identity, categorisation,
 * worker mapping, and workspace-flag gating.
 */
export class McpToolsManifestLoader {
    constructor(private readonly extensionPath: string) {}

    load(): McpToolsCatalogData {
        const manifest: McpToolsManifest = JSON.parse(
            readFileSync(join(this.extensionPath, 'mcp-tools-manifest.json'), 'utf-8'),
        );

        const categoryByName = new Map(manifest.categories.map(c => [c.name, c]));
        const serverLabelOrder: string[] = [];
        const categoryOrder: string[] = [];
        const serverLabelToWorkerIdMap = new Map<string, string>();

        for (const category of manifest.categories) {
            if (category.workerId) {
                serverLabelOrder.push(category.name);
                serverLabelToWorkerIdMap.set(category.name, category.workerId);
            } else {
                categoryOrder.push(category.name);
            }
        }

        const entries: McpToolsEntry[] = [];
        const descriptions = new Map<string, McpToolsMetadataEntry>();

        for (const tool of manifest.tools) {
            descriptions.set(tool.name, { description: tool.description });

            const serverLabel = tool.categories[0];
            const leafCategory = tool.categories[tool.categories.length - 1];
            McpToolsManifestLoader.#validateToolCategories(tool.name, tool.categories, categoryByName);
            const workspaceFlags = McpToolsManifestLoader.#unionOfWhenFlags(tool.categories, categoryByName);

            if (!tool.tasks || tool.tasks.length === 0) {
                entries.push(McpToolsManifestLoader.#buildEntry(tool.name, undefined, serverLabel, leafCategory, workspaceFlags));
                continue;
            }

            for (const task of tool.tasks) {
                descriptions.set(task.name, { description: task.description });
                entries.push(McpToolsManifestLoader.#buildEntry(task.name, tool.name, serverLabel, leafCategory, workspaceFlags));
            }
        }

        return {
            entries,
            descriptions,
            serverLabelOrder,
            categoryOrder,
            serverLabelToWorkerIdMap,
        };
    }

    static #buildEntry(
        key: string,
        toolName: string | undefined,
        serverLabel: string,
        category: string,
        workspaceFlags: readonly string[],
    ): McpToolsEntry {
        // Per the user-visible UI, raw task names are used as labels — the
        // manifest deliberately doesn't carry separate UI labels.
        return {
            key,
            toolName,
            label: key,
            category,
            serverLabel,
            workspaceFlags: workspaceFlags.length > 0 ? workspaceFlags : undefined,
        };
    }

    static #unionOfWhenFlags(
        categoryNames: readonly string[],
        categoryByName: ReadonlyMap<string, McpCategoryManifest>,
    ): readonly string[] {
        const seen = new Set<string>();

        for (const name of categoryNames) {
            const category = categoryByName.get(name);
            if (!category?.when) { continue; }

            for (const flag of category.when) {
                seen.add(flag);
            }
        }

        return [...seen];
    }

    static #validateToolCategories(
        toolName: string,
        categoryNames: readonly string[],
        categoryByName: ReadonlyMap<string, McpCategoryManifest>,
    ): void {
        if (categoryNames.length === 0) {
            throw new Error(`mcp-tools-manifest.json: tool '${toolName}' has no categories; the first category must be a server-label category (one with a 'workerId').`);
        }

        for (const name of categoryNames) {
            if (!categoryByName.has(name)) {
                throw new Error(`mcp-tools-manifest.json: tool '${toolName}' references unknown category '${name}'.`);
            }
        }

        const serverLabel = categoryNames[0];
        const serverCategory = categoryByName.get(serverLabel)!;
        if (!serverCategory.workerId) {
            throw new Error(`mcp-tools-manifest.json: tool '${toolName}' uses '${serverLabel}' as its server label, but that category has no 'workerId'. The first entry in 'categories' must be a server-label category.`);
        }
    }
}
