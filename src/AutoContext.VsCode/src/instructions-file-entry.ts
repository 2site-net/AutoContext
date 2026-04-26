import { InstructionsFileItemEntry } from './instructions-file-item-entry.js';
import type { InstructionsFileCategoryEntry } from './instructions-file-category-entry.js';
import type { InstructionsFileMetadata } from './types/instructions-file-metadata.js';

/**
 * An instructions file from `resources/instructions-files.json`.
 *
 * `name` is the full filename (e.g. `lang-csharp.instructions.md`).
 * `key` is derived from `name` by stripping the `.instructions.md`
 * suffix and is used to build the user-facing `contextKey`
 * (`autocontext.instructions.<key>`).
 */
export class InstructionsFileEntry extends InstructionsFileItemEntry {
    readonly key: string;
    readonly contextKey: string;
    readonly label: string;
    readonly version?: string;
    readonly hasChangelog: boolean;

    constructor(
        name: string,
        label: string,
        readonly categories: readonly InstructionsFileCategoryEntry[],
        readonly activationFlags?: readonly string[],
        metadata?: InstructionsFileMetadata,
    ) {
        super(name, metadata?.description);
        this.key = name.replace(/\.instructions\.md$/, '');
        this.contextKey = `autocontext.instructions.${this.key}`;
        this.label = label;
        this.version = metadata?.version;
        this.hasChangelog = metadata?.hasChangelog ?? false;
    }

    get firstCategory(): InstructionsFileCategoryEntry {
        return this.categories[0];
    }

    get targetPath(): string {
        return `.github/instructions/${this.name}`;
    }
}
