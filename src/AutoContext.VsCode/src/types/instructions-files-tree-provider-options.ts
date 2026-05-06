import type { InstructionsFilesManifest } from '../instructions-files-manifest.js';
import type { WorkspaceContextDetector } from '../workspace-context-detector.js';
import type { InstructionsFilesOverrideWatcher } from '../instructions-files-override-watcher.js';
import type { TreeViewTooltip } from '../tree-view-tooltip.js';
import type { AutoContextConfigManager } from '../autocontext-config-manager.js';

/**
 * Construction options for `InstructionsFilesTreeProvider`.
 */
export interface InstructionsFilesTreeProviderOptions {
    readonly detector: WorkspaceContextDetector;
    readonly overrideWatcher: InstructionsFilesOverrideWatcher;
    readonly manifest: InstructionsFilesManifest;
    readonly tooltip: TreeViewTooltip;
    readonly configManager: AutoContextConfigManager;
}
