import * as vscode from 'vscode';
import type { ChannelLogger } from 'autocontext-nodejs-core';
import type { InstructionsFileContentProjector } from './instructions-file-content-projector.js';
import type { InstructionsFilesManifest } from './instructions-files-manifest.js';
import type { InstructionsFilesOverrideWatcher } from './instructions-files-override-watcher.js';
import type { InstructionsFileSectionWithOffsets } from './types/instructions-file-section-with-offsets.js';
import type { InstructionsFilesLmToolsContentExcerpt } from './types/instructions-files-lm-tools-content-excerpt.js';
import type { InstructionsFilesLmToolsContentMatch } from './types/instructions-files-lm-tools-content-match.js';

interface FileIndex {
    readonly name: string;
    readonly description: string;
    readonly body: string;
    readonly sections: readonly InstructionsFileSectionWithOffsets[];
    readonly descriptionTokens: ReadonlyMap<string, number>;
    readonly contentTokens: ReadonlyMap<string, number>;
}

/**
 * Free-text body search across the bundled instructions files. Powers
 * `search_autocontext_instructions_files_by_content`. Indexes both
 * `description` and body (via `InstructionsFileContentProjector` so
 * overrides + `.generated/` are already reconciled) into per-file
 * token-frequency maps with an identifier-aware tokenizer that splits
 * on `\W+` *and* camelCase / kebab / snake boundaries, so a query of
 * `ConfigureAwait` matches a heading written as "Configure Await".
 *
 * Match: AND across distinct query tokens — a file matches iff every
 * tokenized query piece appears in either map.
 *
 * Score: per matching query token, `descHits * 2 + contentHits * 1`,
 * summed across distinct query tokens. Ties broken by `name` ascending
 * for deterministic output.
 *
 * Excerpts: up to 3 body slices per hit, ordered by earliest position
 * in the body. Each carries `section` / `sectionLevel` / `anchor`
 * derived from `[charStart, charEnd)` of the parsed section index.
 *
 * Index lifecycle: built lazily on first `search` call (composition is
 * sync; projector reads are async, so an eager build would force the
 * caller to await activation). Override-watcher `onDidChange` is
 * coarse-grained, so the engine simply invalidates the whole index
 * and lets the next `search` rebuild it; full rebuild for ~78 small
 * files is cheap.
 *
 * Disabled files are not filtered here — the projector returns
 * `undefined` for files whose `.generated/` body is absent, and they
 * are silently skipped at index time. Step 7 handlers apply the
 * authoritative `resolveState()` filter on the way out.
 */
export class InstructionsFilesLmToolsContentSearch implements vscode.Disposable {
    private static readonly defaultLimit = 10;
    private static readonly maxLimit = 25;
    private static readonly maxExcerptsPerHit = 3;
    private static readonly excerptRadius = 80;
    private static readonly minTokenLength = 2;
    private static readonly tokenSplitPattern = /\W+/;
    private static readonly identifierBoundaryPattern =
        /(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|[-_]/;

    private indexPromise: Promise<ReadonlyMap<string, FileIndex>> | undefined;
    private readonly subscriptions: vscode.Disposable[] = [];

    constructor(
        private readonly manifest: InstructionsFilesManifest,
        private readonly projector: InstructionsFileContentProjector,
        private readonly overrideWatcher: InstructionsFilesOverrideWatcher,
        private readonly logger: ChannelLogger,
    ) {
        this.subscriptions.push(this.overrideWatcher.onDidChange(() => this.invalidate()));
    }

    async search(
        query: string,
        opts?: { readonly limit?: number },
    ): Promise<readonly InstructionsFilesLmToolsContentMatch[]> {
        const queryTokensFull = this.collectQueryTokens(query);
        if (queryTokensFull.distinct.size === 0) {
            return [];
        }

        const limit = this.normalizeLimit(opts?.limit);
        const index = await this.getIndex();

        const matches: InstructionsFilesLmToolsContentMatch[] = [];
        for (const file of index.values()) {
            const score = this.scoreFile(file, queryTokensFull.distinct);
            if (score <= 0) {
                continue;
            }
            const excerpts = this.extractExcerpts(file, queryTokensFull.distinct);
            matches.push({ name: file.name, score, excerpts });
        }

        matches.sort((a, b) => (b.score - a.score) || a.name.localeCompare(b.name));
        return matches.slice(0, limit);
    }

    dispose(): void {
        for (const d of this.subscriptions) {
            d.dispose();
        }
        this.subscriptions.length = 0;
    }

    private invalidate(): void {
        this.indexPromise = undefined;
    }

    private getIndex(): Promise<ReadonlyMap<string, FileIndex>> {
        if (!this.indexPromise) {
            this.indexPromise = this.buildIndex().catch(err => {
                // On a build failure, leave the cache cleared so the
                // next call can retry rather than memoize a bad state.
                this.indexPromise = undefined;
                throw err;
            });
        }
        return this.indexPromise;
    }

    private async buildIndex(): Promise<ReadonlyMap<string, FileIndex>> {
        const entries = this.manifest.instructions;
        const built = await Promise.all(entries.map(entry => this.buildFileIndex(entry.name, entry.description)));
        const map = new Map<string, FileIndex>();
        for (const file of built) {
            if (file) {
                map.set(file.name, file);
            }
        }
        return map;
    }

    private async buildFileIndex(name: string, description: string | undefined): Promise<FileIndex | undefined> {
        let projection;
        try {
            projection = await this.projector.project(name);
        } catch (err) {
            this.logger.warn(`Content-search index: failed to project '${name}'`, err);
            return undefined;
        }
        if (!projection) {
            return undefined;
        }
        const desc = description ?? '';
        return {
            name,
            description: desc,
            body: projection.body,
            sections: projection.sections,
            descriptionTokens: this.tokenize(desc),
            contentTokens: this.tokenize(projection.body),
        };
    }

    private normalizeLimit(input: number | undefined): number {
        if (input === undefined || !Number.isFinite(input) || input <= 0) {
            return InstructionsFilesLmToolsContentSearch.defaultLimit;
        }
        return Math.min(Math.floor(input), InstructionsFilesLmToolsContentSearch.maxLimit);
    }

    private collectQueryTokens(query: string): {
        readonly distinct: ReadonlySet<string>;
    } {
        const distinct = new Set<string>();
        for (const word of query.split(InstructionsFilesLmToolsContentSearch.tokenSplitPattern)) {
            if (!word) continue;
            for (const piece of this.expandToken(word)) {
                distinct.add(piece);
            }
        }
        return { distinct };
    }

    private tokenize(text: string): ReadonlyMap<string, number> {
        const counts = new Map<string, number>();
        for (const word of text.split(InstructionsFilesLmToolsContentSearch.tokenSplitPattern)) {
            if (!word) continue;
            for (const piece of this.expandToken(word)) {
                counts.set(piece, (counts.get(piece) ?? 0) + 1);
            }
        }
        return counts;
    }

    private expandToken(word: string): readonly string[] {
        const out: string[] = [];
        const whole = word.toLowerCase();
        if (whole.length >= InstructionsFilesLmToolsContentSearch.minTokenLength) {
            out.push(whole);
        }
        for (const piece of word.split(InstructionsFilesLmToolsContentSearch.identifierBoundaryPattern)) {
            const lower = piece.toLowerCase();
            if (lower.length >= InstructionsFilesLmToolsContentSearch.minTokenLength && lower !== whole) {
                out.push(lower);
            }
        }
        return out;
    }

    private scoreFile(file: FileIndex, distinctQueryTokens: ReadonlySet<string>): number {
        let score = 0;
        for (const token of distinctQueryTokens) {
            const descHits = file.descriptionTokens.get(token) ?? 0;
            const contentHits = file.contentTokens.get(token) ?? 0;
            if (descHits === 0 && contentHits === 0) {
                return 0; // AND semantics — every query token must appear somewhere.
            }
            score += descHits * 2 + contentHits;
        }
        return score;
    }

    private extractExcerpts(
        file: FileIndex,
        distinctQueryTokens: ReadonlySet<string>,
    ): readonly InstructionsFilesLmToolsContentExcerpt[] {
        const positions = this.findMatchPositions(file.body, distinctQueryTokens);
        if (positions.length === 0) {
            return [];
        }

        const excerpts: InstructionsFilesLmToolsContentExcerpt[] = [];
        let lastEnd = -1;
        for (const pos of positions) {
            if (excerpts.length >= InstructionsFilesLmToolsContentSearch.maxExcerptsPerHit) {
                break;
            }
            const window = this.sliceWindow(file.body, pos.start, pos.end);
            if (window.start <= lastEnd) {
                continue; // Drop overlapping windows so the model sees distinct context.
            }
            const section = this.findSectionForOffset(file.sections, pos.start);
            excerpts.push({
                text: file.body.slice(window.start, window.end).trim(),
                section: section?.heading ?? '',
                sectionLevel: section?.parent ? 3 : 2,
                anchor: section?.anchor ?? '',
            });
            lastEnd = window.end;
        }
        return excerpts;
    }

    private findMatchPositions(
        body: string,
        distinctQueryTokens: ReadonlySet<string>,
    ): readonly { readonly start: number; readonly end: number }[] {
        const lowerBody = body.toLowerCase();
        const seen = new Set<number>();
        const positions: { start: number; end: number }[] = [];
        // Cap occurrences per token so a single common token can't crowd
        // out other tokens' positions before the overlap-dedup step.
        const maxPerToken = InstructionsFilesLmToolsContentSearch.maxExcerptsPerHit;
        for (const token of distinctQueryTokens) {
            if (token.length === 0) continue;
            let from = 0;
            let found = 0;
            while (found < maxPerToken) {
                const idx = lowerBody.indexOf(token, from);
                if (idx < 0) break;
                if (!seen.has(idx)) {
                    seen.add(idx);
                    positions.push({ start: idx, end: idx + token.length });
                }
                from = idx + token.length;
                found++;
            }
        }
        positions.sort((a, b) => a.start - b.start);
        return positions;
    }

    private sliceWindow(body: string, start: number, end: number): { start: number; end: number } {
        const radius = InstructionsFilesLmToolsContentSearch.excerptRadius;
        let s = Math.max(0, start - radius);
        let e = Math.min(body.length, end + radius);
        // Snap to whitespace boundaries when one is reachable within the
        // radius, so excerpts don't begin or end mid-word.
        while (s > 0 && !/\s/.test(body[s - 1]) && start - s < radius * 2) {
            s--;
        }
        while (e < body.length && !/\s/.test(body[e]) && e - end < radius * 2) {
            e++;
        }
        return { start: s, end: e };
    }

    private findSectionForOffset(
        sections: readonly InstructionsFileSectionWithOffsets[],
        offset: number,
    ): InstructionsFileSectionWithOffsets | undefined {
        // Binary search over `[charStart, charEnd)` half-open intervals.
        // Sections are emitted in document order, so charStart is sorted.
        let lo = 0;
        let hi = sections.length - 1;
        let candidate: InstructionsFileSectionWithOffsets | undefined;
        while (lo <= hi) {
            const mid = (lo + hi) >>> 1;
            const s = sections[mid];
            if (offset < s.charStart) {
                hi = mid - 1;
            } else if (offset >= s.charEnd) {
                lo = mid + 1;
            } else {
                candidate = s;
                break;
            }
        }
        return candidate;
    }
}
