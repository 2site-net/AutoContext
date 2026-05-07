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
