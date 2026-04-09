import type { InstructionsParsedResult } from './instructions-parsed-result.js';

export interface InstructionsFileParsedResult {
    readonly content: string;
    readonly result: InstructionsParsedResult;
}
