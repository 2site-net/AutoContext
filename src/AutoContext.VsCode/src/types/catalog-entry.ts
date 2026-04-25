/**
 * The minimal shape required by polymorphic consumers (context-key
 * gating and chat-instructions manifest generation). Concrete entries
 * carry richer per-domain fields beyond this contract.
 */
export interface CatalogEntry {
    readonly contextKey: string;
    readonly workspaceFlags?: readonly string[];
}
