import type * as vscode from 'vscode';
import type { McpToolsManifest } from '../mcp-tools-manifest.js';
import type { ServersManifest } from '../servers-manifest.js';
import type { AutoContextConfigManager } from '../autocontext-config-manager.js';
import type { Logger } from './logger.js';

/**
 * Construction options for `McpServerProvider`.
 */
export interface McpServerProviderOptions {
    readonly extensionPath: string;
    readonly version: string;
    readonly onDidChange: vscode.Event<void>;
    readonly toolsManifest: McpToolsManifest;
    readonly serversManifest: ServersManifest;
    readonly configManager: AutoContextConfigManager;
    readonly instanceId: string;
    readonly logServiceAddress: string;
    readonly healthMonitorServiceAddress: string;
    readonly workerControlServiceAddress: string;
    readonly extensionConfigServiceAddress: string;
    readonly logger: Logger;
}
