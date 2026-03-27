import * as vscode from 'vscode';
import { join } from 'node:path';
import { servers } from './server-entry.js';
import { instructionsCatalog } from './instructions-catalog.js';
import { tools } from './tool-entry.js';
import { toolSettingsForScope } from './tools-catalog.js';
import { StatusBarIndicator } from './status-bar-indicator.js';
import { WorkspaceContextDetector } from './workspace-context-detector.js';
import { ToolsStatusWriter } from './tools-status-writer.js';
import { MenuToggler } from './menu-toggler.js';
import { autoConfigure } from './auto-configurer.js';
import { InstructionsExporter } from './instructions-exporter.js';
import { InstructionsBrowser } from './instructions-browser.js';
import { getUnexportedInstructions } from './instructions-export-state.js';
import { SharpPilotConfigManager } from './sharppilot-config.js';
import { InstructionsContentProvider, instructionScheme } from './instructions-content-provider.js';
import { InstructionsCodeLensProvider, toggleInstructionCommandId, resetInstructionsCommandId } from './instructions-codelens-provider.js';
import { InstructionsDecorationManager } from './instructions-decoration-manager.js';
import { InstructionsWriter } from './instructions-writer.js';
import { InstructionsParser } from './instructions-parser.js';
import { readFileSync } from 'node:fs';

export function activate(context: vscode.ExtensionContext): void {
    if (!vscode.workspace.workspaceFolders?.length) {
        return;
    }

    const serversPath = join(context.extensionPath, 'servers');
    const ext = process.platform === 'win32' ? '.exe' : '';
    const version = context.extension.packageJSON.version as string;
    const didChangeEmitter = new vscode.EventEmitter<void>();

    const statusBarIndicator = new StatusBarIndicator();
    const workspaceContextDetector = new WorkspaceContextDetector();
    const toolsToggler = new MenuToggler('SharpPilot: Toggle Tools', 'Select tools to enable', tools);
    const instructionsExporter = new InstructionsExporter(context.extensionPath);
    const instructionsBrowser = new InstructionsBrowser();
    const configManager = new SharpPilotConfigManager(context.extensionPath, version);
    const toolsStatusWriter = new ToolsStatusWriter(configManager);
    const contentProvider = new InstructionsContentProvider(context.extensionPath, configManager);
    const codeLensProvider = new InstructionsCodeLensProvider(context.extensionPath, configManager);
    const decorationManager = new InstructionsDecorationManager(context.extensionPath, configManager);
    const instructionsWriter = new InstructionsWriter(context.extensionPath, configManager);
    const outputChannel = vscode.window.createOutputChannel('SharpPilot');

    function logDiagnostics(): void {
        outputChannel.clear();
        const warnOnMissingId = configManager.read().diagnostic?.warnOnMissingId === true;

        for (const entry of instructionsCatalog.all) {
            let content: string;
            try {
                content = readFileSync(join(context.extensionPath, 'instructions', entry.fileName), 'utf-8');
            } catch {
                continue;
            }

            const { diagnostics } = InstructionsParser.parse(content);

            for (const d of diagnostics) {
                if (d.kind === 'missing-id' && !warnOnMissingId) {
                    continue;
                }

                outputChannel.appendLine(`[warn] ${entry.fileName}:${d.line + 1} — ${d.message}`);
            }
        }
    }

    toolsStatusWriter.write();
    workspaceContextDetector.detect();
    configManager.removeOrphanedIds();
    instructionsWriter.removeOrphanedStagingDirs();
    instructionsWriter.write();
    logDiagnostics();

    context.subscriptions.push(
        didChangeEmitter,
        outputChannel,
        statusBarIndicator,
        workspaceContextDetector,
        configManager,
        contentProvider,
        codeLensProvider,
        decorationManager,
        instructionsWriter,
        vscode.workspace.registerTextDocumentContentProvider(instructionScheme, contentProvider),
        vscode.languages.registerCodeLensProvider({ scheme: instructionScheme }, codeLensProvider),
        // Status bar
        vscode.commands.registerCommand(StatusBarIndicator.commandId, () => statusBarIndicator.showToggleMenu()),
        // Toggle menus
        vscode.commands.registerCommand('sharppilot.toggleTools', async () => { await toolsToggler.toggle(); statusBarIndicator.update(); }),
        // Instructions management
        vscode.commands.registerCommand('sharppilot.toggleInstructions', async () => {
            const availableInstructions = await getUnexportedInstructions();
            if (availableInstructions.length === 0) {
                await vscode.window.showInformationMessage('All instructions are exported. Delete one to toggle it here again.');
                return;
            }

            const instructionsToggler = new MenuToggler(
                'SharpPilot: Toggle Instructions',
                'Select instructions to enable',
                availableInstructions,
                () => workspaceContextDetector.getOverriddenSettingIds(),
            );

            await instructionsToggler.toggle();
            statusBarIndicator.update();
        }),
        vscode.commands.registerCommand('sharppilot.exportInstructions', () => instructionsExporter.export()),
        vscode.commands.registerCommand('sharppilot.browseInstructions', () => instructionsBrowser.browse()),
        // Workspace auto-configuration (instructions + tools)
        vscode.commands.registerCommand('sharppilot.autoConfigure', async () => { await autoConfigure(workspaceContextDetector); statusBarIndicator.update(); }),
        // CodeLens (internal)
        vscode.commands.registerCommand(toggleInstructionCommandId, (fileName: string, id: string) => {
            configManager.toggleInstruction(fileName, id);
        }),
        vscode.commands.registerCommand(resetInstructionsCommandId, (fileName: string) => {
            configManager.resetInstructions(fileName);
        }),
        configManager.onDidChange(() => logDiagnostics()),
        vscode.window.onDidChangeWindowState(e => {
            if (e.focused) {
                instructionsWriter.write();
            }
        }),
        vscode.workspace.onDidGrantWorkspaceTrust(() => {
            instructionsWriter.write();
        }),
        vscode.workspace.onDidChangeConfiguration(e => {
            if (e.affectsConfiguration('sharppilot.instructions') || e.affectsConfiguration('sharppilot.tools')) {
                statusBarIndicator.update();
            }

            if (e.affectsConfiguration('sharppilot.tools')) {
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
                        s => {
                            const args = ['--scope', s.scope];
                            const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
                            if (workspaceFolder) {
                                args.push('--workspace', workspaceFolder.uri.fsPath);
                            }
                            return new vscode.McpStdioServerDefinition(
                                s.label,
                                join(serversPath, 'SharpPilot', `SharpPilot${ext}`),
                                args,
                                undefined,
                                version,
                            );
                        },
                    );
            },
            resolveMcpServerDefinition: async (server) => server,
        }),
    );
}

export function deactivate(): void {}
