import * as vscode from 'vscode';
import type { InstructionsFilesLmToolsSearchByMetadataHandler } from './instructions-files-lm-tools-search-by-metadata-handler.js';
import type { InstructionsFilesLmToolsListInput } from '#types/instructions-files-lm-tools-list-input.js';
import type { InstructionsFilesLmToolsSearchByMetadataResult } from '#types/instructions-files-lm-tools-search-by-metadata-result.js';

/**
 * Powers `list_autocontext_instructions_files`. Translates its
 * narrow input into a metadata predicate and delegates to
 * `InstructionsFilesLmToolsSearchByMetadataHandler.handle` so the
 * two surfaces share **one** matching engine and stay equivalent by
 * construction (property-style equivalence test in Step 11).
 *
 * Equivalence guarantee:
 *   `list_*({ applyTo: x })` ≡ `_by_metadata({ predicate: { applyTo: x } })`
 *   `list_*({ category: c })` ≡ `_by_metadata({ predicate: { categories: '^<escape(c)>$' } })`
 */
export class InstructionsFilesLmToolsListHandler
    implements vscode.LanguageModelTool<InstructionsFilesLmToolsListInput> {

    private static readonly regexEscapePattern = /[.*+?^${}()|[\]\\]/g;

    constructor(
        private readonly searchByMetadata: InstructionsFilesLmToolsSearchByMetadataHandler,
    ) {}

    async invoke(
        options: vscode.LanguageModelToolInvocationOptions<InstructionsFilesLmToolsListInput>,
        _token: vscode.CancellationToken,
    ): Promise<vscode.LanguageModelToolResult> {
        const result = await this.handle(options.input);
        return new vscode.LanguageModelToolResult([
            new vscode.LanguageModelTextPart(JSON.stringify(result)),
        ]);
    }

    handle(input: InstructionsFilesLmToolsListInput): Promise<InstructionsFilesLmToolsSearchByMetadataResult> {
        const predicate: Record<string, string | number | boolean> = {};
        if (input.applyTo !== undefined) {
            predicate.applyTo = input.applyTo;
        }
        if (input.category !== undefined) {
            predicate.categories = `^${this.escapeRegex(input.category)}$`;
        }
        return this.searchByMetadata.handle({
            predicate,
            includeSections: input.includeSections === true,
        });
    }

    private escapeRegex(value: string): string {
        return value.replace(InstructionsFilesLmToolsListHandler.regexEscapePattern, '\\$&');
    }
}
