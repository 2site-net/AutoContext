export type InstructionsFileParserDiagnosticKind = 'malformed-id' | 'duplicate-id' | 'missing-id';

export interface InstructionsFileParserDiagnostic {
    readonly kind: InstructionsFileParserDiagnosticKind;
    readonly line: number;
    readonly message: string;
}
