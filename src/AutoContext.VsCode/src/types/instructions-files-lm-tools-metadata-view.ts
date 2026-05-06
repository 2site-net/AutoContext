import type { InstructionsFileSection } from './instructions-file-section.js';

/**
 * Flattened, predicate-friendly view of a single instructions file
 * built by handlers from `InstructionsFilesManifest` +
 * `InstructionsFilesMetadata`. The predicate engine
 * (`InstructionsFilesLmToolsMetadataPredicate`) operates exclusively
 * over arrays of these views — it does not depend on runtime entries
 * or on the build-time metadata shape directly.
 */
export interface InstructionsFilesLmToolsMetadataView {
    readonly name: string;
    readonly key: string;
    readonly fileName: string;
    readonly description: string;
    readonly version: string;
    readonly applyTo?: string;
    readonly hasChangelog: boolean;
    readonly categories: readonly string[];
    readonly sections: readonly InstructionsFileSection[];
}
