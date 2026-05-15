import * as vscode from 'vscode';
import type { ChannelLogger } from 'autocontext-nodejs-core';
import { LogCategory } from 'autocontext-nodejs-core';
import { WorkspaceContextDetector } from './workspace-context-detector.js';
import { InstructionsFilesOverrideWatcher } from './instructions-files-override-watcher.js';
import { InstructionsFilesManifestLoader } from './instructions-files-manifest-loader.js';
import type { InstructionsFilesManifest } from './instructions-files-manifest.js';
import { InstructionsFilesExporter } from './instructions-files-exporter.js';
import { AutoContextConfigManager } from './autocontext-config-manager.js';
import { InstructionsViewerDocumentProvider } from './instructions-viewer-document-provider.js';
import { InstructionsViewerCodeLensProvider } from './instructions-viewer-codelens-provider.js';
import { InstructionsViewerDecorationManager } from './instructions-viewer-decoration-manager.js';
import { InstructionsFilesManager } from './instructions-files-manager.js';
import { InstructionsFileSectionsCache } from './instructions-file-sections-cache.js';
import { InstructionsFileContentProjector } from './instructions-file-content-projector.js';
import { InstructionsFilesDiagnosticsReporter } from './instructions-files-diagnostics-reporter.js';
import { InstructionsFilesDiagnosticsRunner } from './instructions-files-diagnostics-runner.js';
import { AutoContextConfigProjector } from './autocontext-config-projector.js';
import { InstructionsFilesTreeProvider } from './instructions-files-tree-provider.js';
import { InstructionsFilesMetadataLoader } from './instructions-files-metadata-loader.js';
import { InstructionsFilesLmToolsApplyToMatcher } from './instructions-files-lm-tools-apply-to-matcher.js';
import { InstructionsFilesLmToolsMetadataPredicate } from './instructions-files-lm-tools-metadata-predicate.js';
import { InstructionsFilesLmToolsMetadataViews } from './instructions-files-lm-tools-metadata-views.js';
import { InstructionsFilesLmToolsContentSearch } from './instructions-files-lm-tools-content-search.js';
import { InstructionsFilesLmToolsListHandler } from './instructions-files-lm-tools-list-handler.js';
import { InstructionsFilesLmToolsSearchByMetadataHandler } from './instructions-files-lm-tools-search-by-metadata-handler.js';
import { InstructionsFilesLmToolsSearchByContentHandler } from './instructions-files-lm-tools-search-by-content-handler.js';
import { InstructionsFilesLmToolsGetHandler } from './instructions-files-lm-tools-get-handler.js';
import { McpToolsManifestLoader } from './mcp-tools-manifest-loader.js';
import { McpToolsTreeProvider } from './mcp-tools-tree-provider.js';
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

        // 1. Core stateful services that entries depend on.
        const configManager = new AutoContextConfigManager(extensionPath, version, log(LogCategory.Config));
        const workspaceContextDetector = new WorkspaceContextDetector(log(LogCategory.Detection));
        const bundledInstructionsNames = InstructionsFilesManifestLoader.loadInstructionNames(extensionPath);
        const instructionsOverrideWatcher = new InstructionsFilesOverrideWatcher(
            bundledInstructionsNames,
            log(LogCategory.Detection),
        );

        // 2. Static manifests / metadata (sync JSON reads).
        const instructionsMetadata = new InstructionsFilesMetadataLoader(extensionPath).load();
        const mcpToolsManifest = new McpToolsManifestLoader(extensionPath, {
            detector: workspaceContextDetector,
            configManager,
        }).load();
        const instructionsManifest: InstructionsFilesManifest = new InstructionsFilesManifestLoader(
            extensionPath,
            {
                detector: workspaceContextDetector,
                overrideWatcher: instructionsOverrideWatcher,
                configManager,
            },
        ).load(instructionsMetadata);
        const serversManifest = new ServersManifestLoader(extensionPath).load();

        const workerIds = new Set(
            mcpToolsManifest.topCategories
                .map(c => c.workerId)
                .filter((id): id is string => id !== undefined),
        );
        const workerEntries = serversManifest.servers.filter(s => workerIds.has(s.id));
        const instructionsExporter = new InstructionsFilesExporter(extensionPath, log(LogCategory.Instructions));
        const instructionsWriter = new InstructionsFilesManager(extensionPath, configManager, instructionsManifest, log(LogCategory.InstructionsWriter));
        const instructionsSectionsCache = new InstructionsFileSectionsCache();
        const instructionsContentProjector = new InstructionsFileContentProjector(
            extensionPath,
            instructionsOverrideWatcher,
            instructionsWriter,
            instructionsSectionsCache,
            log(LogCategory.Instructions),
        );
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
        const codeLensProvider = new InstructionsViewerCodeLensProvider({ extensionPath, configManager, detector: workspaceContextDetector, overrideWatcher: instructionsOverrideWatcher, manifest: instructionsManifest, logger: log(LogCategory.Instructions) });
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

        const instructionsTreeProvider = new InstructionsFilesTreeProvider({
            detector: workspaceContextDetector,
            overrideWatcher: instructionsOverrideWatcher,
            manifest: instructionsManifest,
            tooltip: new TreeViewTooltip('instructions'),
            configManager,
        });
        const mcpToolsTreeProvider = new McpToolsTreeProvider({
            detector: workspaceContextDetector,
            manifest: mcpToolsManifest,
            tooltip: new TreeViewTooltip('tools'),
            configManager,
            logger: log(LogCategory.McpToolsTree),
            healthMonitor,
            serverProvider: mcpServerProvider,
        });

        // 5. Diagnostics.
        const diagnosticsRunner = new InstructionsFilesDiagnosticsRunner(extensionPath, configManager, instructionsManifest);
        const diagnosticsReporter = new InstructionsFilesDiagnosticsReporter(diagnosticsRunner, rootLogger);

        // 6. LM-tool surface (instructions discovery).
        const lmToolsApplyToMatcher = new InstructionsFilesLmToolsApplyToMatcher(log(LogCategory.Instructions));
        const lmToolsMetadataPredicate = new InstructionsFilesLmToolsMetadataPredicate(lmToolsApplyToMatcher);
        const lmToolsMetadataViews = new InstructionsFilesLmToolsMetadataViews(instructionsManifest, instructionsMetadata);
        const lmToolsContentSearch = new InstructionsFilesLmToolsContentSearch(
            instructionsManifest,
            instructionsContentProjector,
            instructionsOverrideWatcher,
            log(LogCategory.Instructions),
        );
        const lmToolsSearchByMetadataHandler = new InstructionsFilesLmToolsSearchByMetadataHandler(
            instructionsManifest,
            lmToolsMetadataViews,
            lmToolsMetadataPredicate,
        );
        const lmToolsListHandler = new InstructionsFilesLmToolsListHandler(lmToolsSearchByMetadataHandler);
        const lmToolsSearchByContentHandler = new InstructionsFilesLmToolsSearchByContentHandler(
            instructionsManifest,
            lmToolsContentSearch,
            lmToolsApplyToMatcher,
            instructionsMetadata,
        );
        const lmToolsGetHandler = new InstructionsFilesLmToolsGetHandler(instructionsManifest, instructionsContentProjector);

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
            instructionsOverrideWatcher,
            configManager,
            contentProvider,
            codeLensProvider,
            decorationManager,
            instructionsWriter,
            configProjector,
            instructionsTreeProvider,
            mcpToolsTreeProvider,
            mcpServerProvider,
            lmToolsContentSearch,
        ];

        return {
            // Manifests
            instructionsManifest,
            mcpToolsManifest,
            serversManifest,
            // Core
            configManager,
            workspaceContextDetector,
            instructionsOverrideWatcher,
            instructionsExporter,
            instructionsWriter,
            instructionsSectionsCache,
            instructionsContentProjector,
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
            // Diagnostics
            diagnosticsRunner,
            diagnosticsReporter,
            // LM tools (instructions discovery)
            lmToolsListHandler,
            lmToolsSearchByMetadataHandler,
            lmToolsSearchByContentHandler,
            lmToolsGetHandler,
            // Lifecycle
            disposables,
        };
    }
}
