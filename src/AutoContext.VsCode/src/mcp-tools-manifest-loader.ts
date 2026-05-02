import { McpCategoryEntry } from './mcp-category-entry.js';
import { McpToolEntry, type McpTaskInit } from './mcp-tool-entry.js';
import { McpToolsManifest } from './mcp-tools-manifest.js';
import { ResourceManifestLoader } from './resource-manifest-loader.js';

interface JsonMcpTask {
    name: string;
    description: string;
}

interface JsonMcpTool {
    name: string;
    description: string;
    categories: readonly string[];
    tasks?: readonly JsonMcpTask[];
}

interface JsonMcpCategory {
    name: string;
    description?: string;
    activationFlags?: readonly string[];
    workerId?: string;
}

interface JsonMcpToolsManifest {
    schemaVersion?: string;
    categories: readonly JsonMcpCategory[];
    tools: readonly JsonMcpTool[];
}

/**
 * Loads `resources/mcp-tools.json` from the extension folder and projects
 * it into a fully-resolved `McpToolsManifest` instance. The manifest is
 * the single source of truth for tool/task identity, categorisation,
 * worker mapping, and workspace-flag gating.
 */
export class McpToolsManifestLoader extends ResourceManifestLoader<JsonMcpToolsManifest, McpToolsManifest> {
    constructor(extensionPath: string) {
        super(extensionPath, 'mcp-tools.json');
    }

    protected project(json: JsonMcpToolsManifest): McpToolsManifest {
        const categories = json.categories.map(c =>
            new McpCategoryEntry(c.name, c.description, c.workerId, c.activationFlags ?? []),
        );
        const categoryByName = new Map<string, McpCategoryEntry>(
            categories.map(c => [c.name, c]),
        );

        const tools = json.tools.map(t => {
            this.validateToolCategories(t.name, t.categories, categoryByName);
            const toolCategories = t.categories.map(name => categoryByName.get(name)!);
            const tasks: readonly McpTaskInit[] = t.tasks?.map(k => ({
                name: k.name,
                description: k.description,
            })) ?? [];
            return new McpToolEntry(t.name, t.description, toolCategories, tasks);
        });

        return new McpToolsManifest(tools, categories);
    }

    private validateToolCategories(
        toolName: string,
        categoryNames: readonly string[],
        categoryByName: ReadonlyMap<string, McpCategoryEntry>,
    ): void {
        if (categoryNames.length === 0) {
            this.fail(`tool '${toolName}' has no categories; the first category must be a top-level category (one with a 'workerId').`);
        }

        for (const name of categoryNames) {
            if (!categoryByName.has(name)) {
                this.fail(`tool '${toolName}' references unknown category '${name}'.`);
            }
        }

        const topLevel = categoryByName.get(categoryNames[0])!;
        if (!topLevel.isTopLevel) {
            this.fail(`tool '${toolName}' uses '${categoryNames[0]}' as its top-level category, but that category has no 'workerId'. The first entry in 'categories' must be a top-level category.`);
        }
    }
}
