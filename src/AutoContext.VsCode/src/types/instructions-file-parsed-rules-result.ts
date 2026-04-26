import type { InstructionsFileParsedRule } from './instructions-file-parsed-rule.js';
import type { InstructionsFileParserDiagnostic } from './instructions-file-parser-diagnostic.js';

export interface InstructionsFileParsedFrontmatter {
    readonly description?: string;
    readonly version?: string;
}

export interface InstructionsFileParsedRulesResult {
    readonly frontmatter: InstructionsFileParsedFrontmatter;
    readonly instructions: readonly InstructionsFileParsedRule[];
    readonly diagnostics: readonly InstructionsFileParserDiagnostic[];
}
