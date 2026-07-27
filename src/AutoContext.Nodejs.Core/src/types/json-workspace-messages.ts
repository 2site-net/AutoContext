// Wire shapes of the Workspace.* surface.

/**
 * Workspace-context flags. The flag set is derived from the engine's
 * detection rule tables, so it grows without a wire-contract change.
 */
export type JsonWorkspaceFlags = Readonly<Record<string, boolean>>;

/** Result of Workspace.Detect. */
export interface JsonWorkspaceDetectResult {
    readonly extensions: readonly string[];
    readonly flags: JsonWorkspaceFlags;
}

/** Result of Workspace.Info. */
export interface JsonWorkspaceInfoResult {
    readonly engineVersion: string;
    readonly idleTimeout: string;
    readonly instanceId: string;
    readonly instanceLabel: string;
    readonly revision: number;
}
