import type { InstructionsFilesLmToolsMetadataMatchResult } from './instructions-files-lm-tools-metadata-match-result.js';

/**
 * Discriminated-union return shape for
 * `InstructionsFilesLmToolsMetadataPredicate.evaluate`. Errors are
 * returned, never thrown — the LM-tool surface must always reply
 * with structured JSON.
 */
export type InstructionsFilesLmToolsMetadataPredicateResult =
    | InstructionsFilesLmToolsMetadataPredicateOk
    | InstructionsFilesLmToolsMetadataPredicateError;

export interface InstructionsFilesLmToolsMetadataPredicateOk {
    readonly kind: 'ok';
    readonly results: readonly InstructionsFilesLmToolsMetadataMatchResult[];
}

export interface InstructionsFilesLmToolsMetadataPredicateError {
    readonly kind: 'error';
    readonly error: 'invalid-regex' | 'unknown-field' | 'pattern-too-long' | 'type-mismatch';
    readonly field: string;
    readonly reason: string;
}
