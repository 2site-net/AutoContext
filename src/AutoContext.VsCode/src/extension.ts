import * as vscode from 'vscode';
import { EXTENSION_NAME } from './ui-constants.js';
import { OutputChannelLogger } from './output-channel-logger.js';
import { IdentifierFactory } from './identifier-factory.js';
import { LogCategory } from 'autocontext-framework-web';
import { ExtensionComposer } from './extension-composition.js';
import { ExtensionRegistrar } from './extension-registrations.js';
import { ExtensionActivator } from './extension-activation.js';

let subscriptions: vscode.Disposable[] | undefined;

/**
 * Extension entry point.
 *
 * Activation is split into four phases for testability and clarity:
 *
 * 1. **Compose** (sync) — `ExtensionComposer.compose()` builds the
 *    entire object graph in a single linear pass, returning a typed graph.
 * 2. **Bootstrap** (async, minimal) — pre-read the config so tree
 *    providers render with real state, then start named-pipe servers.
 * 3. **Register** (sync) — `ExtensionRegistrar.register()` registers
 *    all VS Code commands, providers, and event listeners.
 * 4. **Activate** (async) — `ExtensionActivator.run()` performs
 *    detection, waits on the workspace worker, runs projection /
 *    staging / orphan cleanup, applies the version banner, and emits
 *    initial diagnostics.
 */
export async function activate(context: vscode.ExtensionContext) {
    if (!vscode.workspace.workspaceFolders?.length) {
        return;
    }

    subscriptions = context.subscriptions;

    const version = context.extension.packageJSON.version as string;
    const didChangeEmitter = new vscode.EventEmitter<void>();
    const rootLogger = OutputChannelLogger.create(EXTENSION_NAME);
    const activationLogger = rootLogger.forCategory(LogCategory.Activation);
    activationLogger.info(`Activating AutoContext v${version}`);

    // 1. Compose — build the graph synchronously.
    const graph = new ExtensionComposer({
        extensionPath: context.extensionPath,
        version,
        workspaceRoot: vscode.workspace.workspaceFolders?.[0]?.uri.fsPath,
        instanceId: IdentifierFactory.createInstanceId(),
        didChangeEmitter,
        rootLogger,
    }).compose();
    context.subscriptions.push(...graph.disposables);

    // 2. Bootstrap — pre-read config (so tree providers render with real
    //    state on first query) and start the named-pipe servers.
    await graph.configManager.read();
    await graph.logServer.start();
    await graph.healthMonitor.start();
    await graph.workerControlServer.start();
    await graph.autoContextConfigServer.start();
    activationLogger.debug('Health monitor, worker-control, and config servers started; workers spawn on demand');

    // 3. Register — wire up all VS Code surfaces.
    context.subscriptions.push(
        ...new ExtensionRegistrar({ context, graph, didChangeEmitter, rootLogger }).register(),
    );

    // 4. Activate — async sequence (detection, worker-ready barrier,
    //    projection, version banner, diagnostics).
    await new ExtensionActivator({ context, graph, didChangeEmitter, version, rootLogger }).run();

    return {
        mcpServerProvider: graph.mcpServerProvider,
        configManager: graph.configManager,
        codeLensProvider: graph.codeLensProvider,
        contentProvider: graph.contentProvider,
        workspaceContextDetector: graph.workspaceContextDetector,
        workerManager: graph.workerManager,
        healthMonitor: graph.healthMonitor,
        instructionsTreeProvider: graph.instructionsTreeProvider,
        mcpToolsTreeProvider: graph.mcpToolsTreeProvider,
    };
}

export function deactivate(): void {
    if (subscriptions) {
        for (const d of subscriptions) {
            d.dispose();
        }
        subscriptions = undefined;
    }
}
