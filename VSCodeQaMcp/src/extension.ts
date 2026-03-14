import * as vscode from 'vscode';
import { join } from 'node:path';

export function activate(context: vscode.ExtensionContext): void {
    const serversPath = join(context.extensionPath, 'servers');
    const ext = process.platform === 'win32' ? '.exe' : '';
    const didChangeEmitter = new vscode.EventEmitter<void>();

    context.subscriptions.push(
        didChangeEmitter,
        vscode.lm.registerMcpServerDefinitionProvider('qaMcpProvider', {
            onDidChangeMcpServerDefinitions: didChangeEmitter.event,
            provideMcpServerDefinitions: async () => [
                new vscode.McpStdioServerDefinition(
                    'DotNet QA MCP',
                    join(serversPath, 'DotNetQaMcp', `DotNetQaMcp${ext}`),
                    [],
                    undefined,
                    '0.1.0',
                ),
                new vscode.McpStdioServerDefinition(
                    'Git QA MCP',
                    join(serversPath, 'GitQaMcp', `GitQaMcp${ext}`),
                    [],
                    undefined,
                    '0.1.0',
                ),
            ],
            resolveMcpServerDefinition: async (server) => server,
        }),
    );
}

export function deactivate(): void {}
