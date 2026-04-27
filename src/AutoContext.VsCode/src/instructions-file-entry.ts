import { InstructionsFileItemEntry } from './instructions-file-item-entry.js';
import type { InstructionsFileCategoryEntry } from './instructions-file-category-entry.js';
import { InstructionsFileRuntimeInfo } from './instructions-file-runtime-info.js';
import type { InstructionsFileMetadata } from '#types/instructions-file-metadata.js';

/**
 * An instructions file from `resources/instructions-files.json`.
 *
 * `name` is the full filename (e.g. `lang-csharp.instructions.md`).
 * `key` is derived from `name` by stripping the `.instructions.md`
 * suffix; runtime context-key concerns live on `runtimeInfo`.
 */
export class InstructionsFileEntry extends InstructionsFileItemEntry {
    readonly key: string;
    readonly #runtimeInfo: InstructionsFileRuntimeInfo;
    readonly label: string;
    readonly version?: string;
    readonly hasChangelog: boolean;

    constructor(
        name: string,
        label: string,
        readonly categories: readonly InstructionsFileCategoryEntry[],
        readonly activationFlags: readonly string[] = [],
        metadata?: InstructionsFileMetadata,
    ) {
        super(name, metadata?.description);
        this.key = name.replace(/\.instructions\.md$/, '');
        this.#runtimeInfo = new InstructionsFileRuntimeInfo(this.key);
        this.label = label;
        this.version = metadata?.version;
        this.hasChangelog = metadata?.hasChangelog ?? false;
    }

    get runtimeInfo(): InstructionsFileRuntimeInfo {
        return this.#runtimeInfo;
    }

    get firstCategory(): InstructionsFileCategoryEntry {
        return this.categories[0];
    }

    get targetPath(): string {
        return `.github/instructions/${this.name}`;
    }
}
