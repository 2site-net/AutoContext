import * as vscode from 'vscode';
import type { ChannelLogger } from 'autocontext-framework-web';
import { LogCategory } from 'autocontext-framework-web';
import { WorkspaceContextDetector } from './workspace-context-detector.js';
import { InstructionsFilesManifestLoader } from './instructions-files-manifest-loader.js';
import { InstructionsFilesExporter } from './instructions-files-exporter.js';
import { AutoContextConfigManager } from './autocontext-config-manager.js';
import { InstructionsViewerDocumentProvider } from './instructions-viewer-document-provider.js';
import { InstructionsViewerCodeLensProvider } from './instructions-viewer-codelens-provider.js';
import { InstructionsViewerDecorationManager } from './instructions-viewer-decoration-manager.js';
import { InstructionsFilesManager } from './instructions-files-manager.js';
import { InstructionsFilesDiagnosticsReporter } from './instructions-files-diagnostics-reporter.js';
import { InstructionsFilesDiagnosticsRunner } from './instructions-files-diagnostics-runner.js';
import { AutoContextConfigProjector } from './autocontext-config-projector.js';
import { InstructionsFilesTreeProvider } from './instructions-files-tree-provider.js';
import { InstructionsFileMetadataReader } from './instructions-file-metadata-reader.js';
import { McpToolsManifestLoader } from './mcp-tools-manifest-loader.js';
import { McpToolsTreeProvider } from './mcp-tools-tree-provider.js';
import { TreeViewStateResolver } from './tree-view-state-resolver.js';
import { TreeViewTooltip } from './tree-view-tooltip.js';
import { McpServerProvider } from './mcp-server-provider.js';
import { WorkerManager } from './worker-manager.js';
import { ServersManifestLoader } from './servers-manifest-loader.js';
import { HealthMonitorServer } from './health-monitor-server.js';
import { LogServer } from './log-server.js';
import { WorkerControlServer } from './worker-control-server.js';
import { AutoContextConfigServer } from './autocontext-config-server.js';
import type { CompositionInputs } from './types/composition-inputs.js';

/**
 * The complete wired extension graph returned by
 * `ExtensionComposer.compose()`.
 *
 * Tests can construct one of these with fakes/stubs and exercise the
 * activation sequence or registration step in isolation, without
 * needing `vi.mock` for module-level wiring.
 */
export type ExtensionGraph = ReturnType<ExtensionComposer['compose']>;

/**
 * Pure construction phase of extension activation.
 *
 * Builds every long-lived collaborator in a single, linear, synchronous
 * pass. Does NOT start named-pipe servers, register VS Code surfaces,
 * read the config from disk, or perform workspace detection — those
 * are activation-sequence concerns owned by `ExtensionActivator` and
 * `ExtensionRegistrar`.
 *
 * Disposables are surfaced via the `disposables` array; the caller
 * (`activate()`) is responsible for pushing them onto
 * `context.subscriptions` so VS Code drives teardown.
 */
export class ExtensionComposer {
    constructor(private readonly inputs: CompositionInputs) {}

    compose() {
        const { extensionPath, version, workspaceRoot, instanceId, didChangeEmitter, rootLogger } = this.inputs;
        const log = (cat: LogCategory): ChannelLogger => rootLogger.forCategory(cat);

        // 1. Static manifests / metadata (sync JSON reads).
        const metadataReader = new InstructionsFileMetadataReader(extensionPath);
        const mcpToolsManifest = new McpToolsManifestLoader(extensionPath).load();
        const instructionsManifest = new InstructionsFilesManifestLoader(extensionPath)
            .load(metadataReader.readMetadata());
        const serversManifest = new ServersManifestLoader(extensionPath).load();

        const workerIds = new Set(
            mcpToolsManifest.topCategories
                .map(c => c.workerId)
                .filter((id): id is string => id !== undefined),
        );
        const workerEntries = serversManifest.servers.filter(s => workerIds.has(s.id));

        // 2. Core stateful services.
        const configManager = new AutoContextConfigManager(extensionPath, version, log(LogCategory.Config));
        const workspaceContextDetector = new WorkspaceContextDetector(instructionsManifest, log(LogCategory.Detection));
        const instructionsExporter = new InstructionsFilesExporter(extensionPath, log(LogCategory.Instructions));
        const instructionsWriter = new InstructionsFilesManager(extensionPath, configManager, instructionsManifest, log(LogCategory.InstructionsWriter));
        const configProjector = new AutoContextConfigProjector(configManager, instructionsManifest, mcpToolsManifest, log(LogCategory.ConfigProjector));

        // 3. Named-pipe servers (constructed; not started).
        const logServer = new LogServer(log(LogCategory.LogServer), instanceId);
        const healthMonitor = new HealthMonitorServer(log(LogCategory.HealthMonitor), instanceId);
        const workerManager = new WorkerManager({
            extensionPath,
            logger: log(LogCategory.WorkerManager),
            workspaceRoot,
            workers: workerEntries,
            instanceId,
            logServiceAddress: logServer.getPipeName(),
            healthMonitorServiceAddress: healthMonitor.getPipeName(),
        });
        const workerControlServer = new WorkerControlServer(workerManager, workerEntries, instanceId, log(LogCategory.WorkerControl));
        const autoContextConfigServer = new AutoContextConfigServer(configManager, instanceId, log(LogCategory.ConfigServer));

        // 4. VS Code-facing providers.
        const contentProvider = new InstructionsViewerDocumentProvider(extensionPath, configManager, log(LogCategory.Instructions));
        const codeLensProvider = new InstructionsViewerCodeLensProvider({ extensionPath, configManager, detector: workspaceContextDetector, manifest: instructionsManifest, logger: log(LogCategory.Instructions) });
        const decorationManager = new InstructionsViewerDecorationManager(extensionPath, configManager, log(LogCategory.Decorations));
        const mcpServerProvider = new McpServerProvider({
            extensionPath,
            version,
            onDidChange: didChangeEmitter.event,
            toolsManifest: mcpToolsManifest,
            serversManifest,
            configManager,
            instanceId,
            logServiceAddress: logServer.getPipeName(),
            healthMonitorServiceAddress: healthMonitor.getPipeName(),
            workerControlServiceAddress: workerControlServer.getPipeName(),
            extensionConfigServiceAddress: autoContextConfigServer.getPipeName(),
            logger: log(LogCategory.McpServerProvider),
        });

        const stateResolver = new TreeViewStateResolver(workspaceContextDetector);
        const instructionsTreeProvider = new InstructionsFilesTreeProvider({
            detector: workspaceContextDetector,
            manifest: instructionsManifest,
            stateResolver,
            tooltip: new TreeViewTooltip('instructions'),
            configManager,
            logger: log(LogCategory.InstructionsTree),
        });
        const mcpToolsTreeProvider = new McpToolsTreeProvider({
            detector: workspaceContextDetector,
            manifest: mcpToolsManifest,
            stateResolver,
            tooltip: new TreeViewTooltip('tools'),
            configManager,
            logger: log(LogCategory.McpToolsTree),
            healthMonitor,
            serverProvider: mcpServerProvider,
        });

        // 5. Diagnostics.
        const diagnosticsRunner = new InstructionsFilesDiagnosticsRunner(extensionPath, configManager, instructionsManifest);
        const diagnosticsReporter = new InstructionsFilesDiagnosticsReporter(diagnosticsRunner, rootLogger);

        // Disposables that activate() should push onto context.subscriptions.
        // Order matches the original extension.ts for behavioural parity.
        const disposables: readonly vscode.Disposable[] = [
            didChangeEmitter,
            rootLogger,
            logServer,
            healthMonitor,
            workerControlServer,
            autoContextConfigServer,
            workerManager,
            workspaceContextDetector,
            configManager,
            contentProvider,
            codeLensProvider,
            decorationManager,
            instructionsWriter,
            configProjector,
            instructionsTreeProvider,
            mcpToolsTreeProvider,
            mcpServerProvider,
        ];

        return {
            // Manifests
            instructionsManifest,
            mcpToolsManifest,
            serversManifest,
            // Core
            configManager,
            workspaceContextDetector,
            instructionsExporter,
            instructionsWriter,
            configProjector,
            // Named-pipe servers
            logServer,
            healthMonitor,
            workerControlServer,
            autoContextConfigServer,
            workerManager,
            // VS Code-facing
            contentProvider,
            codeLensProvider,
            decorationManager,
            mcpServerProvider,
            instructionsTreeProvider,
            mcpToolsTreeProvider,
            stateResolver,
            // Diagnostics
            diagnosticsRunner,
            diagnosticsReporter,
            // Lifecycle
            disposables,
        };
    }
}
