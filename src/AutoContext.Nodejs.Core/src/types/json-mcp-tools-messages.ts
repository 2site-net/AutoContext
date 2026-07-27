// Wire shapes of the McpTools.* surface.

/** One row of McpTools.List. */
export interface JsonMcpToolsListRow {
    readonly key?: string;
    readonly name?: string;
    readonly description?: string;
    readonly workerId?: string;
    readonly category?: string;
    readonly disabled: boolean;
}

/** Result of McpTools.List. */
export interface JsonMcpToolsListResult {
    readonly tools: readonly JsonMcpToolsListRow[];
}

/** Parameters of McpTools.Invoke. */
export interface JsonMcpToolsInvokeParams {
    readonly name?: string;
    readonly arguments?: unknown;
}

/** One schema violation reported for a tool call's arguments. */
export interface JsonMcpToolsSchemaError {
    readonly path?: string;
    readonly message?: string;
}

/** Result of McpTools.Invoke. */
export type JsonMcpToolsInvokeResult =
    | {
        readonly kind: 'ok';
        readonly name?: string;
        readonly content: readonly unknown[];
        readonly isError?: boolean;
    }
    | {
        readonly kind: 'tool-error';
        readonly name?: string;
        readonly content: readonly unknown[];
        readonly isError: boolean;
    }
    | {
        readonly kind: 'schema-error';
        readonly name?: string;
        readonly errors: readonly JsonMcpToolsSchemaError[];
    }
    | { readonly kind: 'disabled'; readonly name?: string }
    | { readonly kind: 'not-found'; readonly name?: string };
