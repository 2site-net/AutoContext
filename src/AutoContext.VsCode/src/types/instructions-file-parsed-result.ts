import type { InstructionsFileParsedRulesResult } from './instructions-file-parsed-rules-result.js';

export interface InstructionsFileParsedResult {
    readonly content: string;
    readonly result: InstructionsFileParsedRulesResult;
}
