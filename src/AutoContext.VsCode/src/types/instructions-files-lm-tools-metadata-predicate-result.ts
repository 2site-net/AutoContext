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
    /**
     * Self-describing schema attached to every error so the LLM
     * caller can correct the predicate without an extra round-trip.
     * Lists every recognised field with its expected JSON value type
     * and match semantics.
     */
    readonly recognizedFields: readonly InstructionsFilesLmToolsMetadataPredicateFieldInfo[];
}

/**
 * One row of the recognised-field schema returned with every
 * predicate error envelope.
 *
 * - `type` is the JSON value type expected for the predicate value
 *   (`string`, `number`, or `boolean`).
 * - `match` describes how the value is interpreted:
 *   - `regex`         — case-insensitive regular expression
 *   - `glob`          — workspace glob (currently only `applyTo`)
 *   - `equality`      — exact value equality
 *   - `contains-regex`— regex matched against any element of an
 *                       array-valued field (currently only
 *                       `categories`)
 */
export interface InstructionsFilesLmToolsMetadataPredicateFieldInfo {
    readonly field: string;
    readonly type: 'string' | 'number' | 'boolean';
    readonly match: 'regex' | 'glob' | 'equality' | 'contains-regex';
}
