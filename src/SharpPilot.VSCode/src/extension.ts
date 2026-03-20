import * as vscode from 'vscode';
import { join } from 'node:path';
import { servers, instructions, tools, toolSettingsForScope } from './config';
import { StatusBarIndicator } from './status-bar-indicator';
import { WorkspaceContextDetector } from './workspace-context-detector';
import { ToolsStatusWriter } from './tools-status-writer';
import { MenuToggler } from './menu-toggler';
import { InstructionExporter } from './instruction-exporter';
import { InstructionVersionChecker } from './instruction-version-checker';
import { InstructionBrowser } from './instruction-browser';

export function activate(context: vscode.ExtensionContext): void {
    const serversPath = join(context.extensionPath, 'servers');
    const ext = process.platform === 'win32' ? '.exe' : '';
    const version = context.extension.packageJSON.version as string;
    const didChangeEmitter = new vscode.EventEmitter<void>();

    const statusBarIndicator = new StatusBarIndicator();
    const workspaceContextDetector = new WorkspaceContextDetector();
    const toolsStatusWriter = new ToolsStatusWriter(serversPath);
    const instructionsToggler = new MenuToggler('SharpPilot: Toggle Instructions', 'Select instructions to enable', instructions, () => workspaceContextDetector.getOverriddenSettingIds());
    const toolsToggler = new MenuToggler('SharpPilot: Toggle Tools', 'Select tools to enable', tools);
    const instructionExporter = new InstructionExporter(context.extensionPath);
    const instructionVersionChecker = new InstructionVersionChecker(context.extensionPath);
    const instructionBrowser = new InstructionBrowser(context.extensionPath);

    toolsStatusWriter.write();
    workspaceContextDetector.detect();
    instructionVersionChecker.check();

    context.subscriptions.push(
        didChangeEmitter,
        statusBarIndicator,
        workspaceContextDetector,
        vscode.commands.registerCommand(StatusBarIndicator.commandId, () => statusBarIndicator.showToggleMenu()),
        vscode.commands.registerCommand('sharp-pilot.toggleTools', () => toolsToggler.toggle()),
        vscode.commands.registerCommand('sharp-pilot.toggleInstructions', () => instructionsToggler.toggle()),
        vscode.commands.registerCommand('sharp-pilot.exportInstructions', () => instructionExporter.export()),
        vscode.commands.registerCommand('sharp-pilot.browseInstructions', () => instructionBrowser.browse()),
        vscode.workspace.onDidChangeConfiguration(e => {
            if (e.affectsConfiguration('sharp-pilot.instructions') || e.affectsConfiguration('sharp-pilot.tools')) {
                statusBarIndicator.update();
            }

            if (e.affectsConfiguration('sharp-pilot.tools')) {
                toolsStatusWriter.write();
                didChangeEmitter.fire();
            }
        }),
        workspaceContextDetector.onDidChange(() => didChangeEmitter.fire()),
        vscode.lm.registerMcpServerDefinitionProvider('sharpPilotProvider', {
            onDidChangeMcpServerDefinitions: didChangeEmitter.event,
            provideMcpServerDefinitions: async () => {
                const config = vscode.workspace.getConfiguration();
                return servers
                    .filter(s => {
                        if (s.contextKey && !workspaceContextDetector.get(s.contextKey)) {
                            return false;
                        }
                        const toolSettings = toolSettingsForScope(s.scope);
                        return toolSettings.length === 0 || toolSettings.some(id => config.get(id) !== false);
                    })
                    .map(
                        s =>
                            new vscode.McpStdioServerDefinition(
                                s.label,
                                join(serversPath, 'SharpPilot', `SharpPilot${ext}`),
                                ['--scope', s.scope],
                                undefined,
                                version,
                            ),
                    );
            },
            resolveMcpServerDefinition: async (server) => server,
        }),
    );
}

export function deactivate(): void {}
