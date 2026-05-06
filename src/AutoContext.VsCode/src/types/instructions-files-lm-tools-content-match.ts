import type { InstructionsFilesLmToolsContentExcerpt } from './instructions-files-lm-tools-content-excerpt.js';

/**
 * One row of `InstructionsFilesLmToolsContentSearch.search`'s output:
 * the file `name`, an integer `score` (description hits weighted 2×,
 * content hits 1×), and up to 3 body-attributed excerpts ordered by
 * earliest occurrence in the body.
 *
 * Ties on `score` are broken deterministically by `name` ascending,
 * so the engine output is stable across runs and indexes.
 */
export interface InstructionsFilesLmToolsContentMatch {
    readonly name: string;
    readonly score: number;
    readonly excerpts: readonly InstructionsFilesLmToolsContentExcerpt[];
}
