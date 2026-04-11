import type { CatalogEntry } from './types/catalog-entry.js';
import type { InstructionsFileEntry } from './types/instructions-file-entry.js';

export class InstructionsCatalogEntry implements CatalogEntry {
    readonly settingId: string;
    readonly fileName: string;
    readonly label: string;
    readonly category: string;
    readonly contextKeys?: readonly string[];

    constructor(data: InstructionsFileEntry) {
        this.settingId = `sharppilot.instructions.${data.key}`;
        this.fileName = data.fileName;
        this.label = data.label;
        this.category = data.category;
        this.contextKeys = data.contextKeys;
    }

    get targetPath(): string {
        return `.github/instructions/${this.fileName}`;
    }
}
