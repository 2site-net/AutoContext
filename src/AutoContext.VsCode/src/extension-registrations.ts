import * as vscode from 'vscode';
import { join } from 'node:path';
import { LogCategory } from 'autocontext-framework-web';
import { commandIds } from './ui-constants.js';
import { AutoConfigurer } from './auto-configurer.js';
import { instructionScheme } from './instructions-viewer-document-provider.js';
import { InstructionsFilesTreeProvider } from './instructions-files-tree-provider.js';
import { OutputChannelLogger } from './output-channel-logger.js';
import type { RegistrationInputs } from './types/registration-inputs.js';

/**
 * Registers all VS Code surfaces (commands, providers, listeners) for
 * the extension graph.
 *
 * Pure registration — no `await`, no I/O. The caller is responsible
 * for pushing the returned disposables onto `context.subscriptions`.
 *
 * Kept separate from `ExtensionComposer` so the construction phase
 * remains framework-agnostic (no calls into `vscode.commands.*` etc.)
 * and the registration phase remains free of `new` calls for the
 * graph's collaborators.
 */
export class ExtensionRegistrar {
    constructor(private readonly inputs: RegistrationInputs) {}

    register(): readonly vscode.Disposable[] {
        const { context, graph, didChangeEmitter, rootLogger } = this.inputs;
        const {
            configManager,
            instructionsTreeProvider,
            mcpToolsTreeProvider,
            instructionsExporter,
            instructionsWriter,
            contentProvider,
            codeLensProvider,
            mcpServerProvider,
            instructionsManifest,
            mcpToolsManifest,
            workspaceContextDetector,
            diagnosticsReporter,
            lmToolsListHandler,
            lmToolsSearchByMetadataHandler,
            lmToolsSearchByContentHandler,
            lmToolsGetHandler,
        } = graph;

        const diagnosticsLogger = rootLogger.forCategory(LogCategory.Diagnostics);
        const writerLogger = rootLogger.forCategory(LogCategory.InstructionsWriter);

        // "Show / Hide not detected" persistence + tree-provider toggle.
        const showNotDetected = context.globalState.get<boolean>(commandIds.ShowNotDetected, true);
        instructionsTreeProvider.showNotDetected = showNotDetected;
        mcpToolsTreeProvider.showNotDetected = showNotDetected;
        void vscode.commands.executeCommand('setContext', commandIds.ShowNotDetected, showNotDetected);

        const setShowNotDetected = async (value: boolean): Promise<void> => {
            await context.globalState.update(commandIds.ShowNotDetected, value);
            instructionsTreeProvider.showNotDetected = value;
            mcpToolsTreeProvider.showNotDetected = value;
            void vscode.commands.executeCommand('setContext', commandIds.ShowNotDetected, value);
        };

        const logDiagnostics = (): Promise<void> => diagnosticsReporter.report();

        return [
            // MCP server provider — registered early so tools appear in the
            // picker immediately. detect() refines which servers are returned.
            vscode.lm.registerMcpServerDefinitionProvider('AutoContextProvider', mcpServerProvider),

            // Export-mode commands (instructions tree).
            vscode.commands.registerCommand(commandIds.EnterExportMode, () => instructionsTreeProvider.enterExportMode()),
            vscode.commands.registerCommand(commandIds.CancelExport, () => instructionsTreeProvider.cancelExportMode()),
            vscode.commands.registerCommand(commandIds.ConfirmExport, async () => {
                const entries = instructionsTreeProvider.getCheckedEntries();
                instructionsTreeProvider.cancelExportMode();
                await instructionsExporter.export(entries);
            }),

            // Document / code lens for the instructions viewer.
            vscode.workspace.registerTextDocumentContentProvider(instructionScheme, contentProvider),
            vscode.languages.registerCodeLensProvider({ scheme: instructionScheme }, codeLensProvider),

            // Workspace auto-configuration.
            vscode.commands.registerCommand(commandIds.AutoConfigure, async () => {
                await new AutoConfigurer(workspaceContextDetector, instructionsManifest, mcpToolsManifest, configManager).run();
            }),

            // CodeLens (internal).
            vscode.commands.registerCommand(commandIds.ToggleInstruction, (fileName: string, id: string) =>
                configManager.toggleInstruction(fileName, id, instructionsManifest.findByName(fileName)?.version)),
            vscode.commands.registerCommand(commandIds.ResetInstructions, (fileName: string) =>
                configManager.resetInstructions(fileName)),

            // Diagnostics + writer triggers.
            configManager.onDidChange(() =>
                void logDiagnostics().catch(err =>
                    diagnosticsLogger.error('Failed to log diagnostics', err),
                ),
            ),
            vscode.window.onDidChangeWindowState(e => {
                if (e.focused) {
                    void instructionsWriter.write().catch(err =>
                        writerLogger.error('Failed to write on focus', err),
                    );
                }
            }),
            vscode.workspace.onDidGrantWorkspaceTrust(() => {
                void instructionsWriter.write().catch(err =>
                    writerLogger.error('Failed to write on trust grant', err),
                );
            }),

            // Forward config changes to the MCP provider's didChange event.
            configManager.onDidChange(() => didChangeEmitter.fire()),

            // Tree-node enable / disable / override commands.
            vscode.commands.registerCommand(commandIds.EnableInstruction, (node) => instructionsTreeProvider.enableInstruction(node)),
            vscode.commands.registerCommand(commandIds.DisableInstruction, (node) => instructionsTreeProvider.disableInstruction(node)),
            vscode.commands.registerCommand(commandIds.DeleteOverride, InstructionsFilesTreeProvider.deleteOverride),
            vscode.commands.registerCommand(commandIds.ShowOriginal, InstructionsFilesTreeProvider.showOriginal),

            // Changelog / what's new.
            vscode.commands.registerCommand(commandIds.ShowChangelog, async (node: { entry: { name: string } }) => {
                const changelogName = node.entry.name.replace('.instructions.md', '.CHANGELOG.md');
                const uri = vscode.Uri.file(join(context.extensionPath, 'instructions', changelogName));
                await vscode.commands.executeCommand('markdown.showPreview', uri);
            }),
            vscode.commands.registerCommand(commandIds.ShowWhatsNew, async () => {
                const uri = vscode.Uri.file(join(context.extensionPath, 'CHANGELOG.md'));
                await vscode.commands.executeCommand('markdown.showPreview', uri);
            }),

            // Visibility toggles for "not detected" entries.
            vscode.commands.registerCommand(commandIds.ShowNotDetected, () => setShowNotDetected(true)),
            vscode.commands.registerCommand(commandIds.HideNotDetected, () => setShowNotDetected(false)),

            // MCP-tools tree: force-spawn worker / show server output.
            // Force-spawns the worker behind the clicked top-category
            // tree row. Workers are otherwise lazy (spawned by
            // Mcp.Server on first tool call); this gives the user a
            // way to pre-warm one and matches the expectation that the
            // play button on `.NET` / `Web` / `Workspace` flips that
            // row's status dot from red to green. Mcp.Server is left
            // untouched — VS Code starts it on the first tool call.
            vscode.commands.registerCommand(commandIds.StartMcpWorker, async (node?: { workerId?: string }) => {
                if (!node?.workerId) { return; }
                try {
                    await graph.workerManager.ensureRunningById(node.workerId);
                }
                catch (err) {
                    const message = err instanceof Error ? err.message : String(err);
                    void vscode.window.showErrorMessage(`Failed to start worker '${node.workerId}': ${message}`);
                }
            }),
            vscode.commands.registerCommand(commandIds.ShowMcpServerOutput, async (node?: { name?: string; workerId?: string }) => {
                // In MCP Tools top-category rows, the user expects
                // "show output" to open the selected worker channel
                // (e.g. `AutoContext: Worker.DotNet`).
                if (node?.workerId) {
                    const workerEntry = graph.serversManifest.servers.find(s => s.id === node.workerId);
                    if (workerEntry) {
                        const channelName = `AutoContext: ${workerEntry.getShortName()}`;
                        const logger = rootLogger.forChannel(channelName);
                        if (logger instanceof OutputChannelLogger) {
                            logger.show(true);
                            return;
                        }
                    }
                }

                // Fallback: open the MCP server output channel.
                if (!node?.name) { return; }
                for (const id of mcpServerProvider.getDefinitionIds(node.name)) {
                    await vscode.commands.executeCommand('workbench.mcp.showOutput', id);
                }
            }),

            // Language Model tools — instructions discovery surface.
            // Tool names must match the `name` fields declared in
            // package.json's `contributes.languageModelTools` block.
            vscode.lm.registerTool('list_autocontext_instructions_files', lmToolsListHandler),
            vscode.lm.registerTool('search_autocontext_instructions_files_by_metadata', lmToolsSearchByMetadataHandler),
            vscode.lm.registerTool('search_autocontext_instructions_files_by_content', lmToolsSearchByContentHandler),
            vscode.lm.registerTool('get_autocontext_instructions_file', lmToolsGetHandler),
        ];
    }
}
