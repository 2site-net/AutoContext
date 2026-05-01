import * as vscode from 'vscode';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import type { Logger } from '#types/logger.js';
import { LogCategory } from '#types/logger.js';
import type { ExtensionGraph } from './extension-composition.js';
import type { WorkerManager } from './worker-manager.js';
import { contextKeys, globalStateKeys } from './ui-constants.js';

/**
 * Soft timeout for the `Worker.Workspace` ready barrier during activation.
 *
 * Activation must not hang indefinitely if the workspace worker fails to
 * signal ready; downstream steps will continue with whatever state is
 * available, and a warning is logged.
 */
const WORKSPACE_READY_TIMEOUT_MS = 30_000;

/**
 * Async portion of extension activation.
 *
 * Runs after `composeExtension()` has wired the graph and after
 * `registerExtensionSurfaces()` has registered VS Code surfaces. Owns
 * detection, the worker-ready barrier, projection, staging cleanup,
 * version-aware disabled-id sweep, the first instructions write, and
 * the initial diagnostics report.
 *
 * Phases are deliberately sequential where there is a true ordering
 * dependency, and parallel within a phase otherwise.
 */
export async function runActivationSequence(
    context: vscode.ExtensionContext,
    graph: ExtensionGraph,
    didChangeEmitter: vscode.EventEmitter<void>,
    version: string,
    rootLogger: Logger,
): Promise<void> {
    const activationLog = rootLogger.forCategory(LogCategory.Activation);

    // Phase A — workspace detection. Populates `setContext` flags
    // (hasDotNet, hasTypeScript, …) the MCP provider consults.
    await graph.workspaceContextDetector.detect();

    // detect() updates workspaceContextDetector state and the
    // `setContext` flags the MCP provider keys off, but it does not
    // go through configManager — so the configManager.onDidChange
    // forwarder wired in registerExtensionSurfaces() does not fire
    // on detection results. Notify VS Code explicitly so it re-queries
    // the MCP provider with the full set of detected servers.
    didChangeEmitter.fire();

    // Phase B — wait for Worker.Workspace ready (soft timeout).
    await waitForWorkspaceReady(graph.workerManager, activationLog);

    // Phase C — independent fan-out: projection, staging cleanup,
    // orphan-id sweep. None depend on each other; run in parallel.
    await Promise.all([
        graph.configProjector.project(),
        graph.instructionsWriter.removeOrphanedStagingDirs(),
        graph.configManager.removeOrphanedIds(),
    ]);

    // Phase D — version-aware cleanup that depends on projection
    // having run (above) and on the manifest's catalog versions.
    const catalogVersions = new Map(
        graph.instructionsManifest.instructions
            .filter(e => e.version !== undefined)
            .map(e => [e.name, e.version!] as const),
    );
    const clearedFiles = await graph.configManager.clearStaleDisabledIds(catalogVersions);
    if (clearedFiles.length > 0) {
        const names = clearedFiles.map(f => f.replace('.instructions.md', '')).join(', ');
        void vscode.window.showInformationMessage(
            `AutoContext: Disabled instructions cleared for ${names} (version updated).`,
        );
    }

    // Phase E — first instructions write + version-banner state.
    await graph.instructionsWriter.write();

    applyVersionBanner(context, graph, version);

    await graph.diagnosticsReporter.report();

    activationLog.info('Activation complete');
}

async function waitForWorkspaceReady(workerManager: WorkerManager, log: Logger): Promise<void> {
    let timedOut = false;
    let timeoutHandle: ReturnType<typeof setTimeout> | undefined;

    await Promise.race([
        workerManager.whenWorkspaceReady()
            .then(() => clearTimeout(timeoutHandle))
            // If the manager is disposed before the worker signals ready,
            // whenWorkspaceReady() rejects. Activation has already moved
            // on (or will, via the timeout branch); absorb the rejection.
            .catch(() => clearTimeout(timeoutHandle)),
        new Promise<void>(resolve => {
            timeoutHandle = setTimeout(() => {
                timedOut = true;
                resolve();
            }, WORKSPACE_READY_TIMEOUT_MS);
        }),
    ]);

    if (timedOut) {
        log.warn('Timed out waiting for Worker.Workspace ready marker; continuing activation.');
    } else {
        log.debug('Worker.Workspace ready');
    }
}

function applyVersionBanner(
    context: vscode.ExtensionContext,
    graph: ExtensionGraph,
    version: string,
): void {
    const lastSeenVersion = context.globalState.get<string>(globalStateKeys.LastSeenVersion);
    const hasUpdate = lastSeenVersion !== undefined && lastSeenVersion !== version;

    if (hasUpdate) {
        graph.instructionsTreeProvider.setBadge(1, 'New version available');
        graph.instructionsTreeProvider.dismissBadgeOnNextReveal(async () => {
            await context.globalState.update(globalStateKeys.LastSeenVersion, version);
        });
    }

    if (lastSeenVersion === undefined) {
        void context.globalState.update(globalStateKeys.LastSeenVersion, version);
    }

    const hasWhatsNew = existsSync(join(context.extensionPath, 'CHANGELOG.md'));
    void vscode.commands.executeCommand('setContext', contextKeys.HasWhatsNew, hasWhatsNew);
}
