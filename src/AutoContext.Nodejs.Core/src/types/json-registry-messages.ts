// Wire shapes of the Engine.RegistryEntries surface.

/** One engine's row in the shared liveness registry. */
export interface JsonRegistryEntry {
    readonly engineVersion: string;
    readonly workspaceHash: string;
    readonly workspacePath: string;
    readonly instanceId: string;
    readonly instanceLabel: string;
    readonly processId: number;
    readonly processStartTimeUtc: string;
    readonly startedAt: string;
    readonly retention: string;
}

/** Result of Engine.RegistryEntries. */
export interface JsonRegistryEntriesResult {
    readonly entries: readonly JsonRegistryEntry[];
}
