import * as vscode from 'vscode';
import { join } from 'node:path';
import type { McpServersCatalog } from './mcp-servers-catalog.js';
import type { McpToolsCatalog } from './mcp-tools-catalog.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import type { WorkspaceServerManager } from './workspace-server-manager.js';

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
    ) {
        this.serversPath = join(extensionPath, 'mcp');
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
                const toolSettings = this.toolsCatalog.getSettingIdByCategory(s.category);
                return toolSettings.length === 0 || toolSettings.some(id => config.get(id) !== false);
            })
            .map(s => {
                const workspaceFolder = vscode.workspace.workspaceFolders?.[0];

                let command: string;
                const args: string[] = [];

                if (s.process === 'web') {
                    command = 'node';
                    args.push(join(this.serversPath, 'AutoContext.Mcp.Web', 'index.js'));
                } else if (s.process === 'workspace') {
                    command = join(this.serversPath, 'AutoContext.WorkspaceServer', `AutoContext.WorkspaceServer${this.ext}`);
                } else {
                    command = join(this.serversPath, 'AutoContext.Mcp.DotNet', `AutoContext.Mcp.DotNet${this.ext}`);
                }

                args.push('--scope', s.category);

                if (workspaceFolder) {
                    args.push('--workspace-folder', workspaceFolder.uri.fsPath);
                }

                // Some MCP server tools require EditorConfig data from the workspace-server
                // sidecar. We pass the --workspace-server pipe only to non-editorconfig
                // servers, since the editorconfig MCP server tool reads .editorconfig files
                // directly from the workspace.
                // Giving it the same pipe would create a circular dependency.
                if (s.category !== 'editorconfig') {
                    const pipeName = this.workspaceServer.getPipeName();

                    if (pipeName) {
                        args.push('--workspace-server', pipeName);
                    }
                }

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
}
