/**
 * Free-form predicate input passed straight to
 * `InstructionsFilesLmToolsMetadataPredicate.evaluate`. The predicate
 * engine validates field names and value kinds; this handler does
 * not pre-validate so that error envelopes flow back to the LLM with
 * full structured detail (`unknown-field`, `type-mismatch`,
 * `invalid-regex`, `pattern-too-long`).
 */
export interface InstructionsFilesLmToolsSearchByMetadataInput {
    readonly predicate?: Readonly<Record<string, string | number | boolean>>;
    readonly includeSections?: boolean;
}
