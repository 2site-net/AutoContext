import type { InstructionsFilesLmToolsMetadataView } from './instructions-files-lm-tools-metadata-view.js';

/**
 * One row of `InstructionsFilesLmToolsMetadataPredicate.evaluate`'s
 * success output. `matchedAnchors` is populated only when the
 * predicate touched a `sections.*` field — it lists the anchors of
 * the sections that satisfied the predicate (AND across all
 * `sections.*` clauses), so the model can chain
 * `get_autocontext_instructions_file({ sections: matchedAnchors })`.
 */
export interface InstructionsFilesLmToolsMetadataMatchResult {
    readonly view: InstructionsFilesLmToolsMetadataView;
    readonly matchedAnchors?: readonly string[];
}
