// Wire shapes of the Discovery.* surface.

/** Parameters of Discovery.RouteForPrompt. */
export interface JsonDiscoveryRouteForPromptParams {
    readonly prompt?: string;
}

/** Result of Discovery.RouteForPrompt. */
export interface JsonDiscoveryRouteForPromptResult {
    readonly instructions: readonly string[];
    readonly matchedCategories: readonly string[];
    readonly matchedExtensions: readonly string[];
    readonly tools: readonly string[];
}

/** Parameters of Discovery.RouteForTool. */
export interface JsonDiscoveryRouteForToolParams {
    readonly name?: string;
}

/** Result of Discovery.RouteForTool. */
export interface JsonDiscoveryRouteForToolResult {
    readonly instructions: readonly string[];
}
