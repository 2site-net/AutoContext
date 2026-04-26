/**
 * Optional per-instruction metadata loaded at runtime (description,
 * version, changelog presence) keyed by file name.
 */
export interface InstructionsFileMetadata {
    readonly description?: string;
    readonly version?: string;
    readonly hasChangelog?: boolean;
}
