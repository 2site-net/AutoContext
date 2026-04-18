import type { CatalogEntry } from './types/catalog-entry.js';
import type { InstructionsFileEntry } from './types/instructions-file-entry.js';

export interface InstructionsMetadataEntry {
    readonly description?: string;
    readonly version?: string;
    readonly hasChangelog?: boolean;
}

export class InstructionsCatalogEntry implements CatalogEntry {
    readonly contextKey: string;
    readonly fileName: string;
    readonly label: string;
    readonly category: string;
    readonly workspaceFlags?: readonly string[];
    readonly description?: string;
    readonly version?: string;
    readonly hasChangelog: boolean;

    constructor(data: InstructionsFileEntry, metadata?: InstructionsMetadataEntry) {
        this.contextKey = `autocontext.instructions.${data.key}`;
        this.fileName = data.fileName;
        this.label = data.label;
        this.category = data.category;
        this.workspaceFlags = data.workspaceFlags;
        this.description = metadata?.description;
        this.version = metadata?.version;
        this.hasChangelog = metadata?.hasChangelog ?? false;
    }

    get targetPath(): string {
        return `.github/instructions/${this.fileName}`;
    }
}
