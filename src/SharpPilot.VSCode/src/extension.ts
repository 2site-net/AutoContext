import * as vscode from 'vscode';
import { join } from 'node:path';
import { servers, instructions, tools, toolSettingsForScope } from './config.js';
import { StatusBarIndicator } from './status-bar-indicator.js';
import { WorkspaceContextDetector } from './workspace-context-detector.js';
import { ToolsStatusWriter } from './tools-status-writer.js';
import { MenuToggler } from './menu-toggler.js';
import { autoConfigure } from './auto-configurer.js';
import { InstructionExporter } from './instruction-exporter.js';
import { InstructionVersionChecker } from './instruction-version-checker.js';
import { InstructionBrowser } from './instruction-browser.js';
import { SharpPilotConfigManager } from './sharppilot-config.js';
import { InstructionContentProvider, instructionScheme } from './instruction-content-provider.js';
import { RuleCodeLensProvider, toggleRuleCommandId, resetRulesCommandId } from './rule-codelens-provider.js';
import { RuleDecorationManager } from './rule-decoration-manager.js';
import { RuleOverrideWriter } from './rule-override-writer.js';

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
    const instructionBrowser = new InstructionBrowser();
    const configManager = new SharpPilotConfigManager(context.extensionPath, version);
    const contentProvider = new InstructionContentProvider(context.extensionPath, configManager);
    const codeLensProvider = new RuleCodeLensProvider(context.extensionPath, configManager);
    const decorationManager = new RuleDecorationManager(context.extensionPath, configManager);
    const ruleOverrideWriter = new RuleOverrideWriter(context.extensionPath, configManager);

    toolsStatusWriter.write();
    workspaceContextDetector.detect();
    instructionVersionChecker.check();
    configManager.removeOrphanedHashes();
    ruleOverrideWriter.removeOrphanedStagingDirs();
    ruleOverrideWriter.write();

    context.subscriptions.push(
        didChangeEmitter,
        statusBarIndicator,
        workspaceContextDetector,
        configManager,
        contentProvider,
        codeLensProvider,
        decorationManager,
        ruleOverrideWriter,
        vscode.workspace.registerTextDocumentContentProvider(instructionScheme, contentProvider),
        vscode.languages.registerCodeLensProvider({ scheme: instructionScheme }, codeLensProvider),
        vscode.commands.registerCommand(StatusBarIndicator.commandId, () => statusBarIndicator.showToggleMenu()),
        vscode.commands.registerCommand('sharp-pilot.toggleTools', () => toolsToggler.toggle()),
        vscode.commands.registerCommand('sharp-pilot.toggleInstructions', () => instructionsToggler.toggle()),
        vscode.commands.registerCommand('sharp-pilot.autoConfigure', () => autoConfigure(workspaceContextDetector)),
        vscode.commands.registerCommand('sharp-pilot.exportInstructions', () => instructionExporter.export()),
        vscode.commands.registerCommand('sharp-pilot.browseInstructions', () => instructionBrowser.browse()),
        vscode.commands.registerCommand(toggleRuleCommandId, (fileName: string, hash: string) => {
            configManager.toggleRule(fileName, hash);
        }),
        vscode.commands.registerCommand(resetRulesCommandId, (fileName: string) => {
            configManager.resetRules(fileName);
        }),
        vscode.window.onDidChangeWindowState(e => {
            if (e.focused) {
                ruleOverrideWriter.write();
            }
        }),
        vscode.workspace.onDidGrantWorkspaceTrust(() => {
            ruleOverrideWriter.write();
        }),
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
