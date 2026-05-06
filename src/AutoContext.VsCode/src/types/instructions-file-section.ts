/**
 * Catalog-only descriptor for a section in an instructions file. This is
 * the shape persisted to the build-time JSON metadata. Runtime callers
 * that need section slicing use `InstructionsFileSectionWithOffsets`
 * instead.
 *
 * The heading level (`2` or `3`) is intentionally not stored: it is
 * trivially `parent ? 3 : 2`. Callers derive it on demand.
 */
export interface InstructionsFileSection {
    readonly heading: string;
    readonly anchor: string;
    readonly parent?: string;
}
