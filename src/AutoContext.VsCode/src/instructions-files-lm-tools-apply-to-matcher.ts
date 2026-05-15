import * as vscode from 'vscode';
import type { ChannelLogger } from 'autocontext-nodejs-core';

/**
 * Glob-vs-glob set-intersection check that mirrors how VS Code itself
 * decides which `chatInstructions` attach to a file. Used by:
 *
 * - `InstructionsFilesLmToolsMetadataPredicate` for the `applyTo`
 *   clause of `search_autocontext_instructions_files_by_metadata` and
 *   `list_autocontext_instructions_files`.
 * - The content-search handler (Step 7) as a post-filter when the
 *   caller scopes a free-text query by `applyTo`.
 *
 * Both `userInputGlob` and `instructionApplyTo` may be a single glob
 * or a comma-separated list of globs (the form `chatInstructions`
 * itself accepts, e.g. two-glob lists like razor + razor.cs). Either
 * side "matches" if any of its sub-globs hits.
 *
 * Approach (bounded set intersection via the platform's own matchers):
 * 1. Split `userInputGlob` on commas; enumerate up to `findFilesCap`
 *    workspace files in total via `vscode.workspace.findFiles`,
 *    deduplicating by URI.
 * 2. Build a `DocumentSelector` from the comma-split
 *    `instructionApplyTo` (array of `{ pattern }` filters has OR
 *    semantics in `vscode.languages.match`).
 * 3. Return true on the first candidate URI whose match score is
 *    non-zero.
 *
 * Rationale (kept here so future readers don't re-litigate the design):
 * - `vscode.languages.match` is `DocumentSelector` × `TextDocument`,
 *   not glob × glob; there is no first-party set-intersection
 *   primitive.
 * - Synthesizing two URIs (one per glob) is a single-sample hack, not
 *   intersection.
 * - Workspace-file enumeration is what the editor effectively does
 *   when evaluating `chatInstructions` `applyTo`, so we mirror it.
 * - Never hand-roll a glob library.
 */
export class InstructionsFilesLmToolsApplyToMatcher {
    private static readonly findFilesCap = 50;

    constructor(private readonly logger: ChannelLogger) {}

    async matches(userInputGlob: string, instructionApplyTo: string): Promise<boolean> {
        const inputGlobs = this.splitGlobs(userInputGlob);
        const applyToGlobs = this.splitGlobs(instructionApplyTo);
        if (inputGlobs.length === 0 || applyToGlobs.length === 0) {
            return false;
        }

        const candidates = await this.findCandidates(inputGlobs);
        if (candidates.length === 0) {
            return false;
        }

        const selector: vscode.DocumentSelector = applyToGlobs.map(pattern => ({ pattern }));
        for (const uri of candidates) {
            if (this.uriMatchesSelector(uri, selector)) {
                return true;
            }
        }
        return false;
    }

    private splitGlobs(value: string): readonly string[] {
        return value
            .split(',')
            .map(s => s.trim())
            .filter(s => s.length > 0);
    }

    private async findCandidates(inputGlobs: readonly string[]): Promise<readonly vscode.Uri[]> {
        const seen = new Set<string>();
        const candidates: vscode.Uri[] = [];

        for (const glob of inputGlobs) {
            if (candidates.length >= InstructionsFilesLmToolsApplyToMatcher.findFilesCap) {
                break;
            }
            const remaining = InstructionsFilesLmToolsApplyToMatcher.findFilesCap - candidates.length;
            const batch = await this.findOneGlob(glob, remaining);
            for (const uri of batch) {
                const key = uri.toString();
                if (!seen.has(key)) {
                    seen.add(key);
                    candidates.push(uri);
                    if (candidates.length >= InstructionsFilesLmToolsApplyToMatcher.findFilesCap) {
                        break;
                    }
                }
            }
        }
        return candidates;
    }

    private async findOneGlob(glob: string, max: number): Promise<readonly vscode.Uri[]> {
        // Always go through `findFiles` — even for concrete paths.
        // `findFiles` returns 0-or-1 entries for a concrete path and,
        // crucially, only returns it if the file actually exists in
        // the workspace. Synthesizing a `Uri` via `joinPath` would
        // skip that existence check and break the contract that this
        // matcher returns true only when at least one *real* workspace
        // file matches both sides.
        try {
            return await vscode.workspace.findFiles(glob, undefined, max);
        } catch (err) {
            this.logger.warn(`findFiles failed for glob '${glob}'`, err);
            return [];
        }
    }

    private uriMatchesSelector(uri: vscode.Uri, selector: vscode.DocumentSelector): boolean {
        // `vscode.languages.match` requires a `TextDocument`. A minimal
        // shim with `uri`, `fileName`, and a placeholder `languageId`
        // is sufficient for pattern-only `DocumentFilter`s, which is
        // the only shape we construct above.
        const docShim = {
            uri,
            fileName: uri.fsPath,
            languageId: '',
        } as unknown as vscode.TextDocument;
        return vscode.languages.match(selector, docShim) > 0;
    }
}

