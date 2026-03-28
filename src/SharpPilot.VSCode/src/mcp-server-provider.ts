import * as vscode from 'vscode';
import { join } from 'node:path';
import { servers } from './server-entry.js';
import { toolsCatalog } from './tools-catalog.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';

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
    ) {
        this.serversPath = join(extensionPath, 'servers');
        this.ext = process.platform === 'win32' ? '.exe' : '';
        this.version = version;
        this.onDidChangeMcpServerDefinitions = onDidChange;
    }

    async provideMcpServerDefinitions(): Promise<vscode.McpServerDefinition[]> {
        const config = vscode.workspace.getConfiguration();
        return servers
            .filter(s => {
                if (s.contextKey && !this.workspaceContextDetector.get(s.contextKey)) {
                    return false;
                }
                const toolSettings = toolsCatalog.getSettingIdByCategory(s.category);
                return toolSettings.length === 0 || toolSettings.some(id => config.get(id) !== false);
            })
            .map(s => {
                const args = ['--scope', s.category];
                const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
                if (workspaceFolder) {
                    args.push('--workspace', workspaceFolder.uri.fsPath);
                }
                return new vscode.McpStdioServerDefinition(
                    s.label,
                    join(this.serversPath, 'SharpPilot.Mcp', `SharpPilot.Mcp${this.ext}`),
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
