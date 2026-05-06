import type { InstructionsFileSection } from './instructions-file-section.js';

/**
 * Catalogue row returned by `list_autocontext_instructions_files` and
 * `search_autocontext_instructions_files_by_metadata`. The shape
 * stays flat so the LLM can scan many files cheaply; section detail
 * is opt-in (`includeSections`) or auto-attached when a `sections.*`
 * predicate clause participated (`matchedAnchors`).
 */
export interface InstructionsFilesLmToolsCatalogEntry {
    readonly name: string;
    readonly key: string;
    readonly fileName: string;
    readonly label: string;
    readonly description: string;
    readonly version: string;
    readonly applyTo?: string;
    readonly hasChangelog: boolean;
    readonly categories: readonly string[];
    readonly sections?: readonly InstructionsFileSection[];
    readonly matchedAnchors?: readonly string[];
}
