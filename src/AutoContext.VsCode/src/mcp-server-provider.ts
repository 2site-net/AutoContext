import * as vscode from 'vscode';
import { join } from 'node:path';
import { existsSync } from 'node:fs';
import type { McpServersCatalog } from './mcp-servers-catalog.js';
import type { McpToolsCatalog } from './mcp-tools-catalog.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import type { WorkspaceServerManager } from './workspace-server-manager.js';
import type { HealthMonitorServer } from './health-monitor.js';
import type { McpServerEntry } from './types/mcp-server-entry.js';
import { serverLabelToScopesMap } from './ui-constants.js';
import type { AutoContextConfigManager } from './autocontext-config.js';
import type { AutoContextConfig } from './types/autocontext-config.js';
import { isToolEnabled } from './config-context-projector.js';

const extensionId = '2site-net.autocontext';

export class McpServerProvider implements vscode.McpServerDefinitionProvider {
    private readonly serversPath: string;
    private readonly ext: string;
    private readonly version: string;
    private _config: AutoContextConfig;
    private readonly disposable: vscode.Disposable;

    readonly onDidChangeMcpServerDefinitions: vscode.Event<void>;

    constructor(
        extensionPath: string,
        version: string,
        private readonly workspaceContextDetector: WorkspaceContextDetector,
        onDidChange: vscode.Event<void>,
        private readonly workspaceServer: WorkspaceServerManager,
        private readonly toolsCatalog: McpToolsCatalog,
        private readonly serversCatalog: McpServersCatalog,
        private readonly healthMonitor: HealthMonitorServer,
        configManager: AutoContextConfigManager,
    ) {
        this.serversPath = join(extensionPath, 'servers');
        this.ext = process.platform === 'win32' ? '.exe' : '';
        this.version = version;
        this._config = configManager.readSync();
        this.onDidChangeMcpServerDefinitions = onDidChange;
        this.disposable = configManager.onDidChange(() => {
            void configManager.read().then(c => { this._config = c; });
        });
    }

    dispose(): void {
        this.disposable.dispose();
    }

    async provideMcpServerDefinitions(): Promise<vscode.McpServerDefinition[]> {
        return this.serversCatalog.all
            .filter(s => {
                if (!this.isBinaryAvailable(s)) {
                    return false;
                }
                if (s.contextKey && !this.workspaceContextDetector.get(s.contextKey)) {
                    return false;
                }
                const toolEntries = this.toolsCatalog.getEntriesByScope(s.scope);
                return toolEntries.length === 0 || toolEntries.some(e => isToolEnabled(this._config, e.toolName, e.featureName));
            })
            .map(s => {
                const workspaceFolder = vscode.workspace.workspaceFolders?.[0];

                let command: string;
                const args: string[] = [];

                if (s.server === 'web') {
                    command = 'node';
                    args.push(join(this.serversPath, 'AutoContext.Mcp.Web', 'index.js'));
                } else if (s.server === 'workspace') {
                    command = join(this.serversPath, 'AutoContext.WorkspaceServer', `AutoContext.WorkspaceServer${this.ext}`);
                } else {
                    command = join(this.serversPath, 'AutoContext.Mcp.DotNet', `AutoContext.Mcp.DotNet${this.ext}`);
                }

                args.push('--scope', s.scope);

                if (workspaceFolder) {
                    args.push('--workspace-folder', workspaceFolder.uri.fsPath);
                }

                // Some MCP server tools require EditorConfig data from the workspace-server
                // sidecar. We pass the --workspace-server pipe only to non-editorconfig
                // servers, since the editorconfig MCP server tool reads .editorconfig files
                // directly from the workspace.
                // Giving it the same pipe would create a circular dependency.
                if (s.scope !== 'editorconfig') {
                    const pipeName = this.workspaceServer.getPipeName();

                    if (pipeName) {
                        args.push('--workspace-server', pipeName);
                    }
                }

                args.push('--health-monitor', this.healthMonitor.getPipeName());

                return new vscode.McpStdioServerDefinition(
                    s.label,
                    command,
                    args,
                    undefined,
                    this.version,
                );
            });
    }

    async resolveMcpServerDefinition(server: vscode.McpServerDefinition): Promise<vscode.McpServerDefinition> {
        return server;
    }

    /**
     * Returns the availability status of a server label for use in UI indicators.
     * - `'unavailable'`: no server binary exists on disk for any scope in the server label.
     * - `'disabled'`: binaries exist but all servers are filtered by context or settings.
     * - `'available'`: at least one server would be launched.
     */
    getServerStatus(serverLabel: string): 'unavailable' | 'disabled' | 'available' {
        const scopes = serverLabelToScopesMap.get(serverLabel);
        if (!scopes) { return 'unavailable'; }

        let anyBinaryExists = false;

        for (const scope of scopes) {
            const serverEntry = this.serversCatalog.all.find(s => s.scope === scope);
            if (!serverEntry) { continue; }

            if (!this.isBinaryAvailable(serverEntry)) { continue; }
            anyBinaryExists = true;

            if (serverEntry.contextKey && !this.workspaceContextDetector.get(serverEntry.contextKey)) { continue; }

            const toolEntries = this.toolsCatalog.getEntriesByScope(scope);
            if (toolEntries.length > 0 && toolEntries.every(e => !isToolEnabled(this._config, e.toolName, e.featureName))) { continue; }

            return 'available';
        }

        return anyBinaryExists ? 'disabled' : 'unavailable';
    }

    private isBinaryAvailable(server: McpServerEntry): boolean {
        if (!existsSync(this.getBinaryPath(server))) { return false; }

        // Web servers need node_modules alongside index.js to be runnable.
        if (server.server === 'web') {
            return existsSync(join(this.serversPath, 'AutoContext.Mcp.Web', 'node_modules'));
        }

        return true;
    }

    private getBinaryPath(server: McpServerEntry): string {
        if (server.server === 'web') {
            return join(this.serversPath, 'AutoContext.Mcp.Web', 'index.js');
        } else if (server.server === 'workspace') {
            return join(this.serversPath, 'AutoContext.WorkspaceServer', `AutoContext.WorkspaceServer${this.ext}`);
        } else {
            return join(this.serversPath, 'AutoContext.Mcp.DotNet', `AutoContext.Mcp.DotNet${this.ext}`);
        }
    }

    /**
     * Returns the VS Code internal definition IDs for all MCP servers under a tree server label.
     * The ID format is `extensionId/serverLabel` as constructed by VS Code's ext host.
     */
    getDefinitionIds(serverLabel: string): string[] {
        const scopes = serverLabelToScopesMap.get(serverLabel);
        if (!scopes) { return []; }

        return scopes
            .map(scope => this.serversCatalog.all.find(s => s.scope === scope))
            .filter((s): s is McpServerEntry => s !== undefined)
            .map(s => `${extensionId}/${s.label}`);
    }
}
