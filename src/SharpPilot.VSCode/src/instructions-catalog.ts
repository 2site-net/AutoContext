import { InstructionsCatalogEntry } from './instructions-catalog-entry.js';
import type { InstructionsFileEntry } from './types/instructions-file-entry.js';

export class InstructionsCatalog {
    private readonly entries: readonly InstructionsCatalogEntry[];
    private readonly byFileName: ReadonlyMap<string, InstructionsCatalogEntry>;

    constructor(data: readonly InstructionsFileEntry[]) {
        this.entries = data.map(d => new InstructionsCatalogEntry(d));
        this.byFileName = new Map(this.entries.map(e => [e.fileName, e]));
    }

    get all(): readonly InstructionsCatalogEntry[] {
        return this.entries;
    }

    get count(): number {
        return this.entries.length;
    }

    findByFileName(fileName: string): InstructionsCatalogEntry | undefined {
        return this.byFileName.get(fileName);
    }
}
