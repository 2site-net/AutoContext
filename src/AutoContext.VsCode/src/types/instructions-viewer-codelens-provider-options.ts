import type { AutoContextConfigManager } from '../autocontext-config-manager.js';
import type { WorkspaceContextDetector } from '../workspace-context-detector.js';
import type { InstructionsFilesManifest } from '../instructions-files-manifest.js';
import type { ChannelLogger } from 'autocontext-framework-web';

/**
 * Construction options for `InstructionsViewerCodeLensProvider`.
 */
export interface InstructionsViewerCodeLensProviderOptions {
    readonly extensionPath: string;
    readonly configManager: AutoContextConfigManager;
    readonly detector: WorkspaceContextDetector;
    readonly manifest: InstructionsFilesManifest;
    readonly logger: ChannelLogger;
}
