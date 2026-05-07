/**
 * Free-text content search input. `applyTo` and `category` are
 * post-filters layered on top of the ranked hits, mirroring the same
 * constraints accepted by `list_*` so a follow-up "narrow this down"
 * call doesn't need a different shape.
 */
export interface InstructionsFilesLmToolsSearchByContentInput {
    readonly query: string;
    readonly applyTo?: string;
    readonly category?: string;
    readonly limit?: number;
}
