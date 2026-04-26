import type { InstructionsFileParsedResult } from './instructions-file-parsed-result.js';

export interface InstructionsFileParsedCachedResult {
    readonly content: string;
    readonly result: InstructionsFileParsedResult;
}
