import * as vscode from 'vscode';
import { join } from 'node:path';
import { servers } from './config';
import { StatusBarIndicator } from './status-bar-indicator';
import { WorkspaceContextDetector } from './workspace-context-detector';
import { ToolsStatusWriter } from './tools-status-writer';
import { InstructionsToggler } from './instructions-toggler';

export function activate(context: vscode.ExtensionContext): void {
    const serversPath = join(context.extensionPath, 'servers');
    const ext = process.platform === 'win32' ? '.exe' : '';
    const version = context.extension.packageJSON.version as string;
    const didChangeEmitter = new vscode.EventEmitter<void>();

    const statusBarIndicator = new StatusBarIndicator();
    const workspaceContextDetector = new WorkspaceContextDetector();
    const toolsStatusWriter = new ToolsStatusWriter(serversPath);
    const instructionsToggler = new InstructionsToggler();

    toolsStatusWriter.write();
    workspaceContextDetector.detect();

    context.subscriptions.push(
        didChangeEmitter,
        statusBarIndicator,
        workspaceContextDetector,
        vscode.commands.registerCommand('sharp-pilot.toggleInstructions', () => instructionsToggler.toggle()),
        vscode.workspace.onDidChangeConfiguration(e => {
            if (e.affectsConfiguration('sharp-pilot.instructions') || e.affectsConfiguration('sharp-pilot.tools')) {
                statusBarIndicator.update();
            }

            if (e.affectsConfiguration('sharp-pilot.tools')) {
                toolsStatusWriter.write();
            }
        }),
        vscode.lm.registerMcpServerDefinitionProvider('sharpPilotProvider', {
            onDidChangeMcpServerDefinitions: didChangeEmitter.event,
            provideMcpServerDefinitions: async () =>
                servers.map(
                    s =>
                        new vscode.McpStdioServerDefinition(
                            s.label,
                            join(serversPath, 'SharpPilot', `SharpPilot${ext}`),
                            ['--scope', s.scope],
                            undefined,
                            version,
                        ),
                ),
            resolveMcpServerDefinition: async (server) => server,
        }),
    );
}

export function deactivate(): void {}
