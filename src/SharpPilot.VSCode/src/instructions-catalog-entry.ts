import type { CatalogEntry } from './catalog-entry.js';

export type InstructionsFileEntry = CatalogEntry & { fileName: string };

export class InstructionsCatalogEntry implements CatalogEntry {
    readonly settingId: string;
    readonly fileName: string;
    readonly label: string;
    readonly category: string;
    readonly contextKeys?: readonly string[];

    constructor(instructionsFile: InstructionsFileEntry) {
        this.settingId = instructionsFile.settingId;
        this.fileName = instructionsFile.fileName;
        this.label = instructionsFile.label;
        this.category = instructionsFile.category;
        this.contextKeys = instructionsFile.contextKeys;
    }

    get targetPath(): string {
        return `.github/instructions/${this.fileName}`;
    }
}
