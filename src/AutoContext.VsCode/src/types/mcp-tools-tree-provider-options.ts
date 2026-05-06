import type { McpToolsManifest } from '../mcp-tools-manifest.js';
import type { WorkspaceContextDetector } from '../workspace-context-detector.js';
import type { TreeViewTooltip } from '../tree-view-tooltip.js';
import type { AutoContextConfigManager } from '../autocontext-config-manager.js';
import type { HealthMonitorServer } from '../health-monitor-server.js';
import type { McpServerProvider } from '../mcp-server-provider.js';
import type { ChannelLogger } from 'autocontext-framework-web';

/**
 * Construction options for `McpToolsTreeProvider`.
 */
export interface McpToolsTreeProviderOptions {
    readonly detector: WorkspaceContextDetector;
    readonly manifest: McpToolsManifest;
    readonly tooltip: TreeViewTooltip;
    readonly configManager: AutoContextConfigManager;
    readonly logger: ChannelLogger;
    readonly healthMonitor?: HealthMonitorServer;
    readonly serverProvider?: McpServerProvider;
}
