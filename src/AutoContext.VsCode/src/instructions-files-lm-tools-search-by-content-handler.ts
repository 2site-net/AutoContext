import * as vscode from 'vscode';
import type { InstructionsFilesLmToolsApplyToMatcher } from './instructions-files-lm-tools-apply-to-matcher.js';
import type { InstructionsFilesLmToolsContentSearch } from './instructions-files-lm-tools-content-search.js';
import type { InstructionsFilesManifest } from './instructions-files-manifest.js';
import type { InstructionsFilesLmToolsContentHit } from './types/instructions-files-lm-tools-content-hit.js';

/**
 * Free-text content search input. `applyTo` and `category` are
 * post-filters layered on top of the ranked hits, mirroring the same
 * constraints accepted by `list_*` so a follow-up "narrow this down"
 * call doesn't need a different shape.
 */
export interface InstructionsFilesLmToolsSearchByContentInput {
    readonly query: string;
    readonly applyTo?: string;
    readonly category?: string;
    readonly limit?: number;
}

export interface InstructionsFilesLmToolsSearchByContentResult {
    readonly kind: 'ok';
    readonly results: readonly InstructionsFilesLmToolsContentHit[];
}

/**
 * Powers `search_autocontext_instructions_files_by_content`.
 * Sequences:
 *
 * 1. `InstructionsFilesLmToolsContentSearch.search(query, limit)` —
 *    ranked + section-attributed excerpts.
 * 2. Drop disabled files via `entry.resolveState().isActive()`
 *    (Step 3: skip, do not envelope, in search surfaces).
 * 3. Apply `category` post-filter (case-sensitive equality on the
 *    catalogue's `categories` array).
 * 4. Apply `applyTo` post-filter via the Step 5 matcher; entries
 *    without an `applyTo` frontmatter declaration are dropped when
 *    the caller specified `applyTo` (no glob ⇒ no overlap to assert).
 *
 * Order matters: the disabled / category gates are O(1) and avoid
 * running `findFiles` for files that were going to be discarded
 * anyway.
 */
export class InstructionsFilesLmToolsSearchByContentHandler
    implements vscode.LanguageModelTool<InstructionsFilesLmToolsSearchByContentInput> {

    constructor(
        private readonly manifest: InstructionsFilesManifest,
        private readonly engine: InstructionsFilesLmToolsContentSearch,
        private readonly applyToMatcher: InstructionsFilesLmToolsApplyToMatcher,
        private readonly metadata: ReadonlyMap<string, { readonly applyTo?: string }>,
    ) {}

    async invoke(
        options: vscode.LanguageModelToolInvocationOptions<InstructionsFilesLmToolsSearchByContentInput>,
        _token: vscode.CancellationToken,
    ): Promise<vscode.LanguageModelToolResult> {
        const result = await this.handle(options.input);
        return new vscode.LanguageModelToolResult([
            new vscode.LanguageModelTextPart(JSON.stringify(result)),
        ]);
    }

    async handle(
        input: InstructionsFilesLmToolsSearchByContentInput,
    ): Promise<InstructionsFilesLmToolsSearchByContentResult> {
        const ranked = await this.engine.search(input.query, { limit: input.limit });
        // Match List/SearchByMetadata's case-insensitive comparison so
        // the `category` parameter behaves identically across surfaces.
        const wantCategory = input.category?.toLowerCase();

        const filtered: InstructionsFilesLmToolsContentHit[] = [];
        for (const match of ranked) {
            const entry = this.manifest.findByName(match.name);
            if (!entry || !entry.resolveState().isActive()) {
                continue;
            }
            if (wantCategory !== undefined
                && !entry.categories.some(c => c.name.toLowerCase() === wantCategory)) {
                continue;
            }
            if (input.applyTo !== undefined) {
                const fileApplyTo = this.metadata.get(match.name)?.applyTo;
                if (fileApplyTo === undefined) {
                    continue;
                }
                if (!await this.applyToMatcher.matches(input.applyTo, fileApplyTo)) {
                    continue;
                }
            }
            filtered.push({
                name: match.name,
                key: entry.key,
                fileName: entry.name,
                description: entry.description ?? '',
                score: match.score,
                excerpts: match.excerpts,
            });
        }
        return { kind: 'ok', results: filtered };
    }
}
