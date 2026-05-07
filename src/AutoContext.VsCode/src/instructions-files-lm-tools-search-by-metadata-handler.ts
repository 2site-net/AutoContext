import * as vscode from 'vscode';
import type { InstructionsFilesLmToolsMetadataPredicate } from './instructions-files-lm-tools-metadata-predicate.js';
import type { InstructionsFilesLmToolsMetadataViews } from './instructions-files-lm-tools-metadata-views.js';
import type { InstructionsFilesManifest } from './instructions-files-manifest.js';
import type { InstructionsFilesLmToolsCatalogEntry } from '#types/instructions-files-lm-tools-catalog-entry.js';
import type { InstructionsFilesLmToolsMetadataView } from '#types/instructions-files-lm-tools-metadata-view.js';
import type { InstructionsFilesLmToolsMetadataPredicateResult } from '#types/instructions-files-lm-tools-metadata-predicate-result.js';
import type { InstructionsFilesLmToolsSearchByMetadataInput } from '#types/instructions-files-lm-tools-search-by-metadata-input.js';
import type { InstructionsFilesLmToolsSearchByMetadataResult } from '#types/instructions-files-lm-tools-search-by-metadata-result.js';

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
