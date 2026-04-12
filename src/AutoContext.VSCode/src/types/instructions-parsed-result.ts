import type { InstructionsParsedInstruction } from './instructions-parsed-instruction.js';
import type { InstructionsDiagnostic } from './instructions-diagnostic.js';

export interface InstructionsFrontmatter {
    readonly description?: string;
    readonly version?: string;
}

export interface InstructionsParsedResult {
    readonly frontmatter: InstructionsFrontmatter;
    readonly instructions: readonly InstructionsParsedInstruction[];
    readonly diagnostics: readonly InstructionsDiagnostic[];
}
