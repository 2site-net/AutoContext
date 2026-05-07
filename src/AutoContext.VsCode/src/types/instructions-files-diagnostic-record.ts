import type { InstructionsFileParserDiagnosticKind } from './instructions-file-parser-diagnostic.js';

/**
 * One record per problem found while validating the bundled instruction
 * files. The `parse-error` variant means the file could not be loaded or
 * tokenised at all; the other kinds mirror
 * {@link InstructionsFileParserDiagnosticKind} and refer to a specific
 * line within the file.
 */
export type InstructionsFilesDiagnosticRecord =
    | { readonly kind: 'parse-error'; readonly entry: string; readonly message: string }
    | { readonly kind: InstructionsFileParserDiagnosticKind; readonly entry: string; readonly line: number; readonly message: string };
