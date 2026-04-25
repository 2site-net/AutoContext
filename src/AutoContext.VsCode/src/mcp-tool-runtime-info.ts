/**
 * Extension-side runtime concerns for an MCP tool or task: the
 * `when`-clause context key VS Code uses to gate visibility. Lives
 * separate from manifest data so the entry classes stay focused on
 * the manifest shape.
 *
 * Override keys belong to instructions (a different domain) and are
 * deliberately NOT modelled here.
 */
export class McpToolRuntimeInfo {
    constructor(readonly name: string) {}

    get contextKey(): string {
        return `autocontext.mcpTools.${this.name}`;
    }
}
