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

export class McpServerProvider implements vscode.McpServerDefinitionProvider {
    private readonly serversPath: string;
    private readonly ext: string;
    private readonly version: string;

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
    ) {
        this.serversPath = join(extensionPath, 'servers');
        this.ext = process.platform === 'win32' ? '.exe' : '';
        this.version = version;
        this.onDidChangeMcpServerDefinitions = onDidChange;
    }

    async provideMcpServerDefinitions(): Promise<vscode.McpServerDefinition[]> {
        const config = vscode.workspace.getConfiguration();
        return this.serversCatalog.all
            .filter(s => {
                if (s.contextKey && !this.workspaceContextDetector.get(s.contextKey)) {
                    return false;
                }
                const toolSettings = this.toolsCatalog.getSettingIdsByScope(s.scope);
                return toolSettings.length === 0 || toolSettings.some(id => config.get(id) !== false);
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

        const config = vscode.workspace.getConfiguration();
        let anyBinaryExists = false;

        for (const scope of scopes) {
            const serverEntry = this.serversCatalog.all.find(s => s.scope === scope);
            if (!serverEntry) { continue; }

            if (!this.isBinaryAvailable(serverEntry)) { continue; }
            anyBinaryExists = true;

            if (serverEntry.contextKey && !this.workspaceContextDetector.get(serverEntry.contextKey)) { continue; }

            const toolSettings = this.toolsCatalog.getSettingIdsByScope(scope);
            if (toolSettings.length > 0 && toolSettings.every(id => config.get(id) === false)) { continue; }

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
}
