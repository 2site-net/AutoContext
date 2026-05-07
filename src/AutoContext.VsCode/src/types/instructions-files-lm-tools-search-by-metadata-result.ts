import type { InstructionsFilesLmToolsCatalogEntry } from './instructions-files-lm-tools-catalog-entry.js';
import type { InstructionsFilesLmToolsMetadataPredicateError } from './instructions-files-lm-tools-metadata-predicate-result.js';

/**
 * Search-by-metadata response envelope. `kind: 'ok'` carries
 * filtered, shaped catalogue rows; `kind: 'error'` is the predicate
 * engine's structured validation error reflected back to the LLM
 * verbatim, with no `results` field, so the client cannot mistake an
 * empty success for an invalid predicate.
 */
export type InstructionsFilesLmToolsSearchByMetadataResult =
    | { readonly kind: 'ok'; readonly results: readonly InstructionsFilesLmToolsCatalogEntry[] }
    | InstructionsFilesLmToolsMetadataPredicateError;
