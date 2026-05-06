import * as vscode from 'vscode';
import type { InstructionsFilesLmToolsMetadataPredicate } from './instructions-files-lm-tools-metadata-predicate.js';
import type { InstructionsFilesLmToolsMetadataViews } from './instructions-files-lm-tools-metadata-views.js';
import type { InstructionsFilesManifest } from './instructions-files-manifest.js';
import type { InstructionsFilesLmToolsCatalogEntry } from './types/instructions-files-lm-tools-catalog-entry.js';
import type { InstructionsFilesLmToolsMetadataView } from './types/instructions-files-lm-tools-metadata-view.js';
import type {
    InstructionsFilesLmToolsMetadataPredicateError,
    InstructionsFilesLmToolsMetadataPredicateResult,
} from './types/instructions-files-lm-tools-metadata-predicate-result.js';

/**
 * Free-form predicate input passed straight to
 * `InstructionsFilesLmToolsMetadataPredicate.evaluate`. The predicate
 * engine validates field names and value kinds; this handler does
 * not pre-validate so that error envelopes flow back to the LLM with
 * full structured detail (`unknown-field`, `type-mismatch`,
 * `invalid-regex`, `pattern-too-long`).
 */
export interface InstructionsFilesLmToolsSearchByMetadataInput {
    readonly predicate?: Readonly<Record<string, string | number | boolean>>;
    readonly includeSections?: boolean;
}

/**
 * Search-by-metadata response envelope. `kind: 'ok'` carries
 * filtered, shaped catalogue rows; `kind: 'error'` is the predicate
 * engine's structured validation error reflected back to the LLM
 * verbatim, with no `results` field, so the client cannot mistake an
 * empty success for an invalid predicate.
 */
export type InstructionsFilesLmToolsSearchByMetadataResult =
    | { readonly kind: 'ok'; readonly results: readonly InstructionsFilesLmToolsCatalogEntry[] }
    | InstructionsFilesLmToolsMetadataPredicateError;

/**
 * Powers `search_autocontext_instructions_files_by_metadata`. Owns no
 * matching logic — composes:
 *
 * 1. `InstructionsFilesLmToolsMetadataViews.getAll()` for the corpus.
 * 2. `InstructionsFilesLmToolsMetadataPredicate.evaluate(...)` for
 *    semantics (regex / glob / boolean / number, AND across keys,
 *    `sections.*` intersection, `applyTo` glob carve-out).
 * 3. `InstructionsFileEntry.resolveState().isActive()` to silently
 *    drop disabled files (Step 3: list/search results don't envelope;
 *    only `get_*` does).
 *
 * `includeSections` adds the catalogue-only section index to each
 * row. `matchedAnchors` is *always* attached when the predicate
 * touched a `sections.*` field, regardless of `includeSections`,
 * because chained `get_*({ sections: matchedAnchors })` is the
 * primary use case for those queries.
 */
export class InstructionsFilesLmToolsSearchByMetadataHandler
    implements vscode.LanguageModelTool<InstructionsFilesLmToolsSearchByMetadataInput> {

    constructor(
        private readonly manifest: InstructionsFilesManifest,
        private readonly views: InstructionsFilesLmToolsMetadataViews,
        private readonly predicate: InstructionsFilesLmToolsMetadataPredicate,
    ) {}

    async invoke(
        options: vscode.LanguageModelToolInvocationOptions<InstructionsFilesLmToolsSearchByMetadataInput>,
        _token: vscode.CancellationToken,
    ): Promise<vscode.LanguageModelToolResult> {
        const result = await this.handle(options.input);
        return new vscode.LanguageModelToolResult([
            new vscode.LanguageModelTextPart(JSON.stringify(result)),
        ]);
    }

    async handle(
        input: InstructionsFilesLmToolsSearchByMetadataInput,
    ): Promise<InstructionsFilesLmToolsSearchByMetadataResult> {
        const predicate = input.predicate ?? {};
        const includeSections = input.includeSections === true;

        const evalResult: InstructionsFilesLmToolsMetadataPredicateResult =
            await this.predicate.evaluate(predicate, this.views.getAll());

        if (evalResult.kind === 'error') {
            return evalResult;
        }

        const touchesSections = Object.keys(predicate).some(k => k.startsWith('sections.'));
        const results: InstructionsFilesLmToolsCatalogEntry[] = [];
        for (const match of evalResult.results) {
            const entry = this.manifest.findByName(match.view.name);
            if (!entry || !entry.resolveState().isActive()) {
                continue;
            }
            results.push(this.shape(match.view, entry.label, includeSections, touchesSections, match.matchedAnchors));
        }
        return { kind: 'ok', results };
    }

    private shape(
        view: InstructionsFilesLmToolsMetadataView,
        label: string,
        includeSections: boolean,
        touchesSections: boolean,
        matchedAnchors: readonly string[] | undefined,
    ): InstructionsFilesLmToolsCatalogEntry {
        return {
            name: view.name,
            key: view.key,
            fileName: view.fileName,
            label,
            description: view.description,
            version: view.version,
            ...(view.applyTo !== undefined ? { applyTo: view.applyTo } : {}),
            hasChangelog: view.hasChangelog,
            categories: view.categories,
            ...(includeSections || touchesSections ? { sections: view.sections } : {}),
            ...(matchedAnchors !== undefined ? { matchedAnchors } : {}),
        };
    }
}
