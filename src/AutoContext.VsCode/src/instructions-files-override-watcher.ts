import * as vscode from 'vscode';
import type { ChannelLogger } from 'autocontext-nodejs-core';
import { InstructionsFileParser } from './instructions-file-parser.js';

const overrideWatchGlob =
    '**/.github/{copilot-instructions.md,instructions/*.instructions.md}';

/**
 * Watches `.github/instructions/*.instructions.md` (and the legacy
 * `.github/copilot-instructions.md`) under the workspace and tracks
 * which bundled instructions files are currently shadowed by a
 * workspace-local copy.
 *
 * Owns:
 *   - the `FileSystemWatcher` for override files,
 *   - the in-memory set of overridden file names + parsed frontmatter
 *     versions,
 *   - the `setContext('autocontext.override.<name>', …)` writes that
 *     drive `chatInstructions` `when`-clauses.
 *
 * Independent of `WorkspaceContextDetector` so the manifest does not
 * need to be wired back into the detector.
 */
export class InstructionsFilesOverrideWatcher implements vscode.Disposable {
    private readonly disposables: vscode.Disposable[] = [];
    private readonly _onDidChange = new vscode.EventEmitter<void>();
    private debounceTimer: ReturnType<typeof setTimeout> | undefined;
    private _overriddenFileNames: Set<string> = new Set<string>();
    private _overrideVersions: Map<string, string | undefined> = new Map<string, string | undefined>();

    readonly onDidChange = this._onDidChange.event;

    constructor(
        private readonly bundledInstructionsNames: ReadonlySet<string>,
        private readonly logger: ChannelLogger,
    ) {
        const watcher = vscode.workspace.createFileSystemWatcher(overrideWatchGlob);

        this.disposables.push(
            watcher,
            watcher.onDidCreate(() => this.scheduleRescan()),
            watcher.onDidDelete(() => this.scheduleRescan()),
            watcher.onDidChange(() => this.scheduleRescan()),
        );
    }

    /** Performs the initial scan; idempotent. */
    async watch(): Promise<void> {
        await this.rescan();
    }

    isOverridden(fileName: string): boolean {
        return this._overriddenFileNames.has(fileName);
    }

    getOverrideVersion(fileName: string): string | undefined {
        return this._overrideVersions.get(fileName);
    }

    getOverriddenFileNames(): ReadonlySet<string> {
        return this._overriddenFileNames;
    }

    private scheduleRescan(): void {
        if (this.debounceTimer !== undefined) {
            clearTimeout(this.debounceTimer);
        }
        this.debounceTimer = setTimeout(() => {
            this.debounceTimer = undefined;
            void this.rescan();
        }, 500);
    }

    private async rescan(): Promise<void> {
        const started = Date.now();
        try {
            const overrideFiles = await vscode.workspace.findFiles(
                '.github/instructions/*.instructions.md', undefined, 50,
            );

            const fileNames = new Set<string>();
            const versions = new Map<string, string | undefined>();
            const decoder = new TextDecoder();

            for (const uri of overrideFiles) {
                const segments = uri.path.split('/');
                const matchName = segments[segments.length - 1];
                if (!this.bundledInstructionsNames.has(matchName)) continue;

                fileNames.add(matchName);
                try {
                    const content = decoder.decode(await vscode.workspace.fs.readFile(uri));
                    versions.set(matchName, InstructionsFileParser.parseFrontmatter(content).version);
                } catch {
                    versions.set(matchName, undefined);
                }
            }

            this._overriddenFileNames = fileNames;
            this._overrideVersions = versions;
            this._onDidChange.fire();

            // Set VS Code context keys (powers chatInstructions when-clauses).
            // Best-effort: failures must not mask a successful scan.
            await Promise.all(
                [...this.bundledInstructionsNames].map(name => {
                    const key = `autocontext.override.${name.replace(/\.instructions\.md$/, '')}`;
                    return vscode.commands.executeCommand('setContext', key, fileNames.has(name));
                }),
            ).catch(err => this.logger.error('Failed to set override context keys', err));

            this.logger.debug(`Override scan complete in ${Date.now() - started}ms: ${fileNames.size} override(s)`);
        } catch (error) {
            this.logger.error('Override scan failed', error);
        }
    }

    dispose(): void {
        if (this.debounceTimer !== undefined) {
            clearTimeout(this.debounceTimer);
        }
        this._onDidChange.dispose();
        this.disposables.forEach(d => d.dispose());
    }
}
