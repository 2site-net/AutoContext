import type { InstructionsFileSection } from './instructions-file-section.js';

export interface InstructionsFileMetadataEntry {
    readonly key: string;
    readonly fileName: string;
    readonly name: string;
    readonly version: string;
    readonly description: string;
    readonly applyTo?: string;
    readonly hasChangelog: boolean;
    readonly contentHash: string;
    readonly sections: readonly InstructionsFileSection[];
}
