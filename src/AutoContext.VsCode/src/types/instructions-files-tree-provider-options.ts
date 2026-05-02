import type { InstructionsFilesManifest } from '../instructions-files-manifest.js';
import type { WorkspaceContextDetector } from '../workspace-context-detector.js';
import type { TreeViewStateResolver } from '../tree-view-state-resolver.js';
import type { TreeViewTooltip } from '../tree-view-tooltip.js';
import type { AutoContextConfigManager } from '../autocontext-config-manager.js';
import type { ChannelLogger } from 'autocontext-framework-web';

/**
 * Construction options for `InstructionsFilesTreeProvider`.
 */
export interface InstructionsFilesTreeProviderOptions {
    readonly detector: WorkspaceContextDetector;
    readonly manifest: InstructionsFilesManifest;
    readonly stateResolver: TreeViewStateResolver;
    readonly tooltip: TreeViewTooltip;
    readonly configManager: AutoContextConfigManager;
    readonly logger: ChannelLogger;
}
