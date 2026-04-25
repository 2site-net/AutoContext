import { McpItemEntry } from './mcp-item-entry.js';
import { McpToolRuntimeInfo } from './mcp-tool-runtime-info.js';
import type { McpToolEntry } from './mcp-tool-entry.js';

/**
 * A task declared under a tool in `mcp-tools-manifest.json`. Each task
 * has its own context key (`autocontext.mcpTools.<tool>.<task>`) and
 * holds a back-reference to its parent tool.
 */
export class McpTaskEntry extends McpItemEntry {
    readonly #runtimeInfo: McpToolRuntimeInfo;

    constructor(
        name: string,
        description: string | undefined,
        readonly tool: McpToolEntry,
    ) {
        super(name, description);
        this.#runtimeInfo = new McpToolRuntimeInfo(`${tool.name}.${name}`);
    }

    get runtimeInfo(): McpToolRuntimeInfo {
        return this.#runtimeInfo;
    }
}
