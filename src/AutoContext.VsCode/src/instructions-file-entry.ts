import { InstructionsFileItemEntry } from './instructions-file-item-entry.js';
import type { InstructionsFileCategoryEntry } from './instructions-file-category-entry.js';
import { InstructionsFileRuntimeInfo } from './instructions-file-runtime-info.js';
import { TreeViewNodeState } from './tree-view-node-state.js';
import type { InstructionsRuntimeContext } from '#types/runtime-context.js';
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
    readonly #runtimeContext: InstructionsRuntimeContext;

    constructor(
        name: string,
        label: string,
        readonly categories: readonly InstructionsFileCategoryEntry[],
        runtimeContext: InstructionsRuntimeContext,
        readonly activationFlags: readonly string[] = [],
        metadata?: InstructionsFileMetadata,
    ) {
        super(name, metadata?.description);
        this.key = name.replace(/\.instructions\.md$/, '');
        this.#runtimeInfo = new InstructionsFileRuntimeInfo(this.key);
        this.label = label;
        this.version = metadata?.version;
        this.hasChangelog = metadata?.hasChangelog ?? false;
        this.#runtimeContext = runtimeContext;
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

    /**
     * Resolves the current activation/configuration state of this
     * instructions file: `NotDetected` if its activation flags are
     * non-empty and none are detected, `Disabled` if the user toggled
     * it off in `autocontext.json`, `Overridden` if a workspace copy
     * shadows the bundled one, otherwise `Enabled`.
     */
    resolveState(): TreeViewNodeState {
        if (this.activationFlags.length > 0 && !this.activationFlags.some(k => this.#runtimeContext.detector.get(k))) {
            return TreeViewNodeState.NotDetected;
        }

        const config = this.#runtimeContext.configManager.readSync();
        if (config.instructions?.[this.name]?.enabled === false) {
            return TreeViewNodeState.Disabled;
        }

        if (this.#runtimeContext.overrideWatcher.isOverridden(this.name)) {
            return TreeViewNodeState.Overridden;
        }

        return TreeViewNodeState.Enabled;
    }
}
