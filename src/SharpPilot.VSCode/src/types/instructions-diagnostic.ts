export type InstructionsDiagnosticKind = 'malformed-id' | 'duplicate-id' | 'missing-id';

export interface InstructionsDiagnostic {
    readonly kind: InstructionsDiagnosticKind;
    readonly line: number;
    readonly message: string;
}
