// Wire shapes of the Config.* surface.

/** Diagnostic preferences block of the config snapshot. */
export interface JsonConfigDiagnostic {
    readonly warnOnMissingId?: boolean;
}

/** Per-rule disabled state inside a config instructions entry. */
export interface JsonConfigInstructionsRule {
    readonly id?: string;
    readonly disabled?: boolean;
}

/** Per-instructions-file entry of the config snapshot. */
export interface JsonConfigInstructionsFile {
    readonly name?: string;
    readonly version?: string;
    readonly disabled?: boolean;
    readonly rules: readonly JsonConfigInstructionsRule[];
}

/** Per-MCP-tool entry of the config snapshot. */
export interface JsonConfigMcpTool {
    readonly name?: string;
    readonly version?: string;
    readonly disabled?: boolean;
}

/** Projection of `.autocontext.json` onto the wire. */
export interface JsonConfigSnapshot {
    readonly version?: string;
    readonly diagnostic?: JsonConfigDiagnostic;
    readonly instructions: readonly JsonConfigInstructionsFile[];
    readonly mcpTools: readonly JsonConfigMcpTool[];
}

/** Parameters of Config.ToggleFile. */
export interface JsonConfigToggleFileParams {
    readonly name?: string;
}

/** Parameters of Config.ToggleRule. */
export interface JsonConfigToggleRuleParams {
    readonly name?: string;
    readonly ruleId?: string;
}

/** One frame of the Config.Subscribe stream. */
export type JsonConfigStreamFrame =
    | { readonly kind: 'snapshot'; readonly snapshot: JsonConfigSnapshot }
    | { readonly kind: 'dropped'; readonly reason: string };
