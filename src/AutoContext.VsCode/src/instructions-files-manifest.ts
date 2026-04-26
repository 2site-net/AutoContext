import type { InstructionsFileCategoryEntry } from './instructions-file-category-entry.js';
import type { InstructionsFileEntry } from './instructions-file-entry.js';

/**
 * Fully-resolved, in-memory representation of
 * `resources/instructions-files.json`. Built by
 * `InstructionsFilesManifestLoader.load()`.
 */
export class InstructionsFilesManifest {
    #byName?: ReadonlyMap<string, InstructionsFileEntry>;

    constructor(
        readonly instructions: readonly InstructionsFileEntry[],
        readonly categories: readonly InstructionsFileCategoryEntry[],
    ) {}

    get count(): number {
        return this.instructions.length;
    }

    findByName(name: string): InstructionsFileEntry | undefined {
        return (this.#byName ??=
            new Map(this.instructions.map(i => [i.name, i]))).get(name);
    }
}
