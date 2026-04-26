import type { InstructionsFileParsedSpan } from './instructions-file-parsed-span.js';
import type { InstructionsFileParserDiagnostic } from './instructions-file-parser-diagnostic.js';

export interface InstructionsFileParsedFrontmatter {
    readonly description?: string;
    readonly version?: string;
}

export interface InstructionsFileParsedResult {
    readonly frontmatter: InstructionsFileParsedFrontmatter;
    readonly instructions: readonly InstructionsFileParsedSpan[];
    readonly diagnostics: readonly InstructionsFileParserDiagnostic[];
}
