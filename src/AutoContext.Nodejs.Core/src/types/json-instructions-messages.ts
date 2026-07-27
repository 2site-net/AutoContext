// Wire shapes of the Instructions.* surface.

/** Whether a projected instructions body came from disk or the bundle. */
export type InstructionsSource = 'bundled' | 'override';

/** Which body Instructions.GetRaw should return. */
export type InstructionsRawSource = 'active' | 'bundled' | 'override';

/** Heading anchor inside an instructions file. */
export interface JsonInstructionsSection {
    readonly heading?: string;
    readonly anchor?: string;
    readonly parent?: string;
}

/** One row of Instructions.List. */
export interface JsonInstructionsListRow {
    readonly key?: string;
    readonly fileName?: string;
    readonly name?: string;
    readonly version?: string;
    readonly description?: string;
    readonly applyTo?: string;
    readonly hasChangelog: boolean;
    readonly contentHash?: string;
    readonly alwaysAttached: boolean;
    readonly label?: string;
    readonly category?: string;
    readonly disabled: boolean;
    readonly source: InstructionsSource;
    readonly overridePath?: string;
    readonly sections?: readonly JsonInstructionsSection[];
}

/** Parameters of Instructions.List. */
export interface JsonInstructionsListParams {
    readonly includeSections?: boolean;
    readonly applyToWorkspaceFilter?: boolean;
    readonly applyToHint?: string;
}

/** Result of Instructions.List. */
export interface JsonInstructionsListResult {
    readonly files: readonly JsonInstructionsListRow[];
}

/** One projected instructions file. */
export interface JsonInstructionsFile {
    readonly name?: string;
    readonly key?: string;
    readonly fileName?: string;
    readonly content?: string;
    readonly sections: readonly JsonInstructionsSection[];
}

/** Result of Instructions.GetAll and Instructions.GetAlwaysAttached. */
export interface JsonInstructionsFilesResult {
    readonly files: readonly JsonInstructionsFile[];
}

/** Parameters of Instructions.Get. */
export interface JsonInstructionsGetParams {
    readonly name?: string;
    readonly sections?: readonly string[];
}

/** Result of Instructions.Get. */
export type JsonInstructionsGetResult =
    | {
        readonly kind: 'ok';
        readonly name?: string;
        readonly key?: string;
        readonly fileName?: string;
        readonly content?: string;
        readonly returnedSections: readonly string[];
        readonly notFoundSections?: readonly string[];
    }
    | { readonly kind: 'disabled'; readonly name?: string; readonly key?: string }
    | { readonly kind: 'not-found'; readonly name?: string };

/** Parameters of Instructions.GetRaw. */
export interface JsonInstructionsGetRawParams {
    readonly name?: string;
    readonly source: InstructionsRawSource;
}

/** Result of Instructions.GetRaw. */
export type JsonInstructionsGetRawResult =
    | {
        readonly kind: 'ok';
        readonly name?: string;
        readonly key?: string;
        readonly source: InstructionsSource;
        readonly content?: string;
    }
    | { readonly kind: 'not-found'; readonly name?: string };

/** Parameters of Instructions.SearchContent. */
export interface JsonInstructionsSearchContentParams {
    readonly query?: string;
    readonly limit?: number;
    readonly includeDisabled?: boolean;
}

/** One matched excerpt inside a content hit. */
export interface JsonInstructionsContentExcerpt {
    readonly anchor?: string;
    readonly snippet?: string;
    readonly line?: number;
}

/** One scored file in a content search. */
export interface JsonInstructionsContentHit {
    readonly name?: string;
    readonly key?: string;
    readonly fileName?: string;
    readonly description?: string;
    readonly score: number;
    readonly excerpts: readonly JsonInstructionsContentExcerpt[];
}

/** Result of Instructions.SearchContent. */
export interface JsonInstructionsSearchContentResult {
    readonly hits: readonly JsonInstructionsContentHit[];
}

/** Parameters of Instructions.SearchByMetadata. */
export interface JsonInstructionsSearchByMetadataParams {
    readonly predicate?: unknown;
    readonly includeSections?: boolean;
}

/** One file matched by a metadata predicate. */
export interface JsonInstructionsMetadataMatch {
    readonly file: JsonInstructionsListRow;
    readonly matchedAnchors?: readonly string[];
}

/** A predicate field the matcher recognises. */
export interface JsonInstructionsMetadataFieldInfo {
    readonly field: string;
    readonly type: string;
    readonly match: string;
}

/** Result of Instructions.SearchByMetadata. */
export type JsonInstructionsSearchByMetadataResult =
    | { readonly kind: 'ok'; readonly results: readonly JsonInstructionsMetadataMatch[] }
    | {
        readonly kind: 'error';
        readonly error: 'unknown-field' | 'type-mismatch' | 'invalid-regex' | 'pattern-too-long';
        readonly field: string;
        readonly reason: string;
        readonly recognizedFields: readonly JsonInstructionsMetadataFieldInfo[];
    };

/** One curatorial category. */
export interface JsonInstructionsCategory {
    readonly name?: string;
    readonly description?: string;
}

/** Result of Instructions.Categories. */
export interface JsonInstructionsCategoriesResult {
    readonly categories: readonly JsonInstructionsCategory[];
}

/** One frame of the Instructions.Subscribe stream. */
export type JsonInstructionsStreamFrame =
    | { readonly kind: 'snapshot'; readonly files: readonly JsonInstructionsListRow[] }
    | { readonly kind: 'dropped'; readonly reason: string };
