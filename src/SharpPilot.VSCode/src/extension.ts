import * as vscode from 'vscode';
import { StatusBarIndicator } from './status-bar-indicator.js';
import { WorkspaceContextDetector } from './workspace-context-detector.js';
import { ToolsStatusWriter } from './tools-status-writer.js';
import { MenuToggler } from './menu-toggler.js';
import { AutoConfigurer } from './auto-configurer.js';
import { InstructionsExporter } from './instructions-exporter.js';
import { InstructionsBrowser } from './instructions-browser.js';
import { InstructionsExportState } from './instructions-export-state.js';
import { SharpPilotConfigManager } from './sharppilot-config.js';
import { InstructionsContentProvider, instructionScheme } from './instructions-content-provider.js';
import { InstructionsCodeLensProvider, toggleInstructionCommandId, resetInstructionsCommandId } from './instructions-codelens-provider.js';
import { InstructionsDecorationManager } from './instructions-decoration-manager.js';
import { InstructionsWriter } from './instructions-writer.js';
import { InstructionsDiagnostics } from './instructions-diagnostics.js';
import { McpServerProvider } from './mcp-server-provider.js';
import { toolsCatalog } from './tools-catalog.js';

export function activate(context: vscode.ExtensionContext): void {
    if (!vscode.workspace.workspaceFolders?.length) {
        return;
    }

    const version = context.extension.packageJSON.version as string;
    const didChangeEmitter = new vscode.EventEmitter<void>();

    const statusBarIndicator = new StatusBarIndicator();
    const workspaceContextDetector = new WorkspaceContextDetector();
    const toolsToggler = new MenuToggler('SharpPilot: Toggle Tools', 'Select tools to enable', toolsCatalog.all);
    const instructionsExporter = new InstructionsExporter(context.extensionPath);
    const instructionsBrowser = new InstructionsBrowser();
    const configManager = new SharpPilotConfigManager(context.extensionPath, version);
    const toolsStatusWriter = new ToolsStatusWriter(configManager);
    const contentProvider = new InstructionsContentProvider(context.extensionPath, configManager);
    const codeLensProvider = new InstructionsCodeLensProvider(context.extensionPath, configManager);
    const decorationManager = new InstructionsDecorationManager(context.extensionPath, configManager);
    const instructionsWriter = new InstructionsWriter(context.extensionPath, configManager);
    const outputChannel = vscode.window.createOutputChannel('SharpPilot');
    const mcpServerProvider = new McpServerProvider(context.extensionPath, version, workspaceContextDetector, didChangeEmitter.event);

    const logDiagnostics = () => InstructionsDiagnostics.log(outputChannel, context.extensionPath, configManager);

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
            const availableInstructions = await InstructionsExportState.getUnexportedFiles();
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
        vscode.commands.registerCommand('sharppilot.autoConfigure', async () => { await AutoConfigurer.configure(workspaceContextDetector); statusBarIndicator.update(); }),
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
        vscode.lm.registerMcpServerDefinitionProvider('sharpPilotProvider', mcpServerProvider),
    );
}

export function deactivate(): void {}
