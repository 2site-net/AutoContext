import type { InstructionsFileMetadataEntry } from './instructions-file-metadata-entry.js';

export interface InstructionsFilesMetadata {
    readonly schemaVersion: string;
    readonly instructions: readonly InstructionsFileMetadataEntry[];
}
