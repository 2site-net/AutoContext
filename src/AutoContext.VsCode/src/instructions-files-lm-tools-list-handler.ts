import * as vscode from 'vscode';
import type {
    InstructionsFilesLmToolsSearchByMetadataHandler,
    InstructionsFilesLmToolsSearchByMetadataResult,
} from './instructions-files-lm-tools-search-by-metadata-handler.js';

/**
 * Catalogue input — the ergonomic surface that wraps the more general
 * `_by_metadata` predicate. `applyTo` is escaped into the predicate
 * as-is (the predicate routes it through Step 5's matcher). `category`
 * becomes a regex-anchored equality clause on `categories`.
 */
export interface InstructionsFilesLmToolsListInput {
    readonly applyTo?: string;
    readonly category?: string;
    readonly includeSections?: boolean;
}

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
