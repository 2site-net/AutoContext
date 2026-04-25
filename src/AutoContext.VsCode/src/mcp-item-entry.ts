/**
 * Common base for any item in the MCP-tools manifest that has a name
 * and (optionally) a description. Concrete subclasses are
 * `McpCategoryEntry`, `McpToolEntry`, and `McpTaskEntry`.
 */
export abstract class McpItemEntry {
    protected constructor(
        readonly name: string,
        readonly description?: string,
    ) {}
}
