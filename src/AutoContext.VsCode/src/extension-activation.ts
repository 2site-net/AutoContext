import * as vscode from 'vscode';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import type { ChannelLogger } from 'autocontext-nodejs-core';
import { LogCategory } from 'autocontext-nodejs-core';
import { contextKeys, globalStateKeys } from './ui-constants.js';
import type { ActivationInputs } from './types/activation-inputs.js';

/**
 * Async portion of extension activation.
 *
 * Runs after `ExtensionComposer.compose()` has wired the graph and
 * after `ExtensionRegistrar.register()` has registered VS Code
 * surfaces. Owns detection, projection, staging cleanup,
 * version-aware disabled-id sweep, the first instructions write, and
 * the initial diagnostics report.
 *
 * Phases are deliberately sequential where there is a true ordering
 * dependency, and parallel within a phase otherwise.
 */
export class ExtensionActivator {
    private readonly activationLog: ChannelLogger;

    constructor(private readonly inputs: ActivationInputs) {
        this.activationLog = inputs.rootLogger.forCategory(LogCategory.Activation);
    }

    async run(): Promise<void> {
        const { graph, didChangeEmitter } = this.inputs;

        // Phase A — workspace detection. Populates `setContext` flags
        // (hasDotNet, hasTypeScript, …) the MCP provider consults.
        await Promise.all([
            graph.workspaceContextDetector.detect(),
            graph.instructionsOverrideWatcher.watch(),
        ]);

        // detect() updates workspaceContextDetector state and the
        // `setContext` flags the MCP provider keys off, but it does not
        // go through configManager — so the configManager.onDidChange
        // forwarder wired in ExtensionRegistrar.register() does not fire
        // on detection results. Notify VS Code explicitly so it re-queries
        // the MCP provider with the full set of detected servers.
        didChangeEmitter.fire();

        // Phase B — independent fan-out: projection, staging cleanup,
        // orphan-id sweep. None depend on each other; run in parallel.
        await Promise.all([
            graph.configProjector.project(),
            graph.instructionsWriter.removeOrphanedStagingDirs(),
            graph.configManager.removeOrphanedIds(),
        ]);

        // Phase C — version-aware cleanup that depends on projection
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

        // Phase D — first instructions write + version-banner state.
        await graph.instructionsWriter.write();

        this.applyVersionBanner();

        await graph.diagnosticsReporter.report();

        this.activationLog.info('Activation complete');
    }

    private applyVersionBanner(): void {
        const { context, graph, version } = this.inputs;
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
}
