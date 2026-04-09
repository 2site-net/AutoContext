import type { InstructionsParsedInstruction } from './instructions-parsed-instruction.js';
import type { InstructionsDiagnostic } from './instructions-diagnostic.js';

export interface InstructionsParsedResult {
    readonly instructions: readonly InstructionsParsedInstruction[];
    readonly diagnostics: readonly InstructionsDiagnostic[];
}
