import type { InstructionsFilesLmToolsContentExcerpt } from './instructions-files-lm-tools-content-excerpt.js';

/**
 * One ranked hit returned by
 * `search_autocontext_instructions_files_by_content`. Wraps the
 * engine's match with the identity + descriptive metadata the LLM
 * needs to chain into `get_autocontext_instructions_file` without a
 * second list/metadata round-trip.
 *
 * `excerpts[i].anchor` is the chained input for
 * `get_autocontext_instructions_file({ name, sections: [anchor] })`.
 */
export interface InstructionsFilesLmToolsContentHit {
    readonly name: string;
    readonly key: string;
    readonly fileName: string;
    readonly description: string;
    readonly score: number;
    readonly excerpts: readonly InstructionsFilesLmToolsContentExcerpt[];
}
