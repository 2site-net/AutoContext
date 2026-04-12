import * as vscode from 'vscode';
import { writeFile, unlink, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { InstructionsParser } from './instructions-parser.js';
import type { AutoContextConfig } from './types/autocontext-config.js';
import type { VersionedDisabledIds } from './types/versioned-disabled-ids.js';

const configFileName = '.autocontext.json';

export class AutoContextConfigManager implements vscode.Disposable {
    private readonly disposables: vscode.Disposable[] = [];
    private readonly didChangeEmitter = new vscode.EventEmitter<void>();
    private cachedConfig: AutoContextConfig | undefined;
    private writeQueue: Promise<void> = Promise.resolve();
    readonly onDidChange = this.didChangeEmitter.event;

    constructor(
        private readonly extensionPath: string,
        private readonly extensionVersion: string,
    ) {
        const watcher = vscode.workspace.createFileSystemWatcher(`**/${configFileName}`);
        watcher.onDidChange(() => this.invalidate());
        watcher.onDidCreate(() => this.invalidate());
        watcher.onDidDelete(() => this.invalidate());
        this.disposables.push(watcher, this.didChangeEmitter);
    }

    private invalidate(): void {
        this.cachedConfig = undefined;
        this.didChangeEmitter.fire();
    }

    async read(): Promise<AutoContextConfig> {
        if (this.cachedConfig !== undefined) {
            return this.cachedConfig;
        }

        const path = this.configPath();
        if (!path) {
            return {};
        }

        try {
            const raw = await readFile(path, 'utf-8');
            const parsed: Record<string, unknown> = JSON.parse(raw);

            if (parsed['mcp-tools']) {
                parsed.mcpTools = parsed['mcp-tools'];
                delete parsed['mcp-tools'];
            }

            this.cachedConfig = parsed as AutoContextConfig;
            return this.cachedConfig;
        } catch {
            return {};
        }
    }

    async getDisabledInstructions(fileName: string): Promise<ReadonlySet<string>> {
        const config = await this.read();
        const entry = config.instructions?.disabled?.[fileName];
        const ids = AutoContextConfigManager.resolveIds(entry);
        return new Set(ids);
    }

    async hasAnyDisabledInstructions(): Promise<boolean> {
        const config = await this.read();
        const disabled = config.instructions?.disabled;
        if (!disabled) {
            return false;
        }
        return Object.values(disabled).some(entry => AutoContextConfigManager.resolveIds(entry).length > 0);
    }

    async toggleInstruction(fileName: string, id: string, instructionVersion?: string): Promise<void> {
        return this.enqueue(async () => {
            const config = await this.read();
            config.instructions ??= {};
            config.instructions.disabled ??= {};

            const entry = config.instructions.disabled[fileName];
            const ids = AutoContextConfigManager.resolveIds(entry);
            const index = ids.indexOf(id);

            if (index >= 0) {
                ids.splice(index, 1);
            } else {
                ids.push(id);
            }

            if (ids.length === 0) {
                delete config.instructions.disabled[fileName];
            } else if (instructionVersion) {
                config.instructions.disabled[fileName] = {
                    version: AutoContextConfigManager.majorMinor(instructionVersion),
                    ids,
                };
            } else if (entry !== undefined && AutoContextConfigManager.isVersioned(entry)) {
                config.instructions.disabled[fileName] = { version: entry.version, ids };
            } else {
                config.instructions.disabled[fileName] = ids;
            }

            // Clean up empty containers.
            if (Object.keys(config.instructions.disabled).length === 0) {
                delete config.instructions.disabled;
            }
            if (Object.keys(config.instructions).length === 0) {
                delete config.instructions;
            }

            await this.writeConfig(config);
        });
    }

    async setDisabledTools(disabledTools: string[]): Promise<void> {
        return this.enqueue(async () => {
            const config = await this.read();
            const currentDisabled = config.mcpTools?.disabled ?? [];

            if (AutoContextConfigManager.arraysEqual(disabledTools, currentDisabled)) {
                return;
            }

            if (disabledTools.length === 0) {
                delete config.mcpTools;
            } else {
                config.mcpTools = { disabled: disabledTools };
            }

            await this.writeConfig(config);
        });
    }

    async resetInstructions(fileName: string): Promise<void> {
        return this.enqueue(async () => {
            const config = await this.read();
            const disabled = config.instructions?.disabled;
            if (!disabled?.[fileName]) {
                return;
            }

            delete disabled[fileName];

            if (Object.keys(disabled).length === 0) {
                delete config.instructions!.disabled;
            }
            if (Object.keys(config.instructions!).length === 0) {
                delete config.instructions;
            }

            await this.writeConfig(config);
        });
    }

    async removeOrphanedIds(): Promise<number> {
        return this.enqueue(async () => {
            const config = await this.read();
            const disabled = config.instructions?.disabled;
            if (!disabled) {
                return 0;
            }

            let removed = 0;

            await Promise.all(Object.entries(disabled).map(async ([fileName, entry]) => {
                const ids = AutoContextConfigManager.resolveIds(entry);
                try {
                    const filePath = join(this.extensionPath, 'instructions', fileName);
                    const content = await readFile(filePath, 'utf-8');
                    const { instructions } = InstructionsParser.parse(content);
                    const validIds = new Set(
                        instructions.map(r => r.id).filter((id): id is string => id !== undefined),
                    );

                    const filtered = ids.filter(id => validIds.has(id));
                    removed += ids.length - filtered.length;

                    if (filtered.length === 0) {
                        delete disabled[fileName];
                    } else {
                        AutoContextConfigManager.setIds(disabled, fileName, entry, filtered);
                    }
                } catch {
                    // File no longer exists — remove all its IDs.
                    removed += ids.length;
                    delete disabled[fileName];
                }
            }));

            if (removed > 0) {
                // Clean up empty containers.
                if (Object.keys(disabled).length === 0) {
                    delete config.instructions!.disabled;
                }
                if (Object.keys(config.instructions!).length === 0) {
                    delete config.instructions;
                }

                await this.writeConfig(config);
            }

            return removed;
        });
    }

    /**
     * Compares stored MAJOR.MINOR versions against live catalog versions.
     * Clears disabled IDs where the version has advanced. Patch-only bumps
     * are no-ops (stored version is already at MAJOR.MINOR granularity).
     * Returns the list of file names whose disabled IDs were cleared.
     */
    async clearStaleDisabledIds(catalogVersions: ReadonlyMap<string, string>): Promise<readonly string[]> {
        return this.enqueue(async () => {
            const config = await this.read();
            const disabled = config.instructions?.disabled;
            if (!disabled) {
                return [];
            }

            const cleared: string[] = [];

            for (const [fileName, entry] of Object.entries(disabled)) {
                if (!AutoContextConfigManager.isVersioned(entry)) {
                    continue;
                }

                const catalogVersion = catalogVersions.get(fileName);
                if (!catalogVersion) {
                    continue;
                }

                const storedMajorMinor = entry.version;
                const catalogMajorMinor = AutoContextConfigManager.majorMinor(catalogVersion);

                if (storedMajorMinor === catalogMajorMinor) {
                    // Same MAJOR.MINOR — IDs are valid, nothing to do.
                    continue;
                }

                // MAJOR or MINOR bumped — IDs may point to different rules.
                delete disabled[fileName];
                cleared.push(fileName);
            }

            if (cleared.length > 0) {
                if (Object.keys(disabled).length === 0) {
                    delete config.instructions!.disabled;
                }
                if (Object.keys(config.instructions!).length === 0) {
                    delete config.instructions;
                }
                await this.writeConfig(config);
            }

            return cleared;
        });
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }

    // Serializes write operations so concurrent callers never interleave read→mutate→write cycles.
    // Uses .then() chaining (not async/await) so the queue is updated synchronously before returning,
    // preventing two callers in the same microtask from reading the old queue and running concurrently.
    private enqueue<T>(fn: () => Promise<T>): Promise<T> {
        const task = this.writeQueue.then(fn);
        // Always resolve so the next operation runs even if this one fails.
        this.writeQueue = task.then(() => {}, () => {});
        return task;
    }

    private configPath(): string | undefined {
        const folder = vscode.workspace.workspaceFolders?.[0];
        if (!folder) {
            return undefined;
        }
        return join(folder.uri.fsPath, configFileName);
    }

    private async writeConfig(config: AutoContextConfig): Promise<void> {
        const path = this.configPath();
        if (!path) {
            return;
        }

        this.cachedConfig = undefined;

        const isEmpty = !config.instructions && !config.diagnostic && !config.mcpTools;
        if (isEmpty) {
            try {
                await unlink(path);
            } catch {
                // File didn't exist — nothing to delete.
            }
            return;
        }

        // Enforce deterministic key order: version first.
        const ordered: AutoContextConfig = { version: this.extensionVersion };
        if (config.diagnostic) ordered.diagnostic = config.diagnostic;
        if (config.instructions) ordered.instructions = config.instructions;
        if (config.mcpTools) ordered.mcpTools = config.mcpTools;

        // Remap camelCase to kebab-case for the on-disk JSON key.
        const { mcpTools, ...rest } = ordered;
        const output = mcpTools ? { ...rest, 'mcp-tools': mcpTools } : rest;

        await writeFile(path, JSON.stringify(output, null, 4) + '\n', 'utf-8');
    }

    private static arraysEqual(a: readonly string[], b: readonly string[]): boolean {
        if (a.length !== b.length) {
            return false;
        }
        for (let i = 0; i < a.length; i++) {
            if (a[i] !== b[i]) {
                return false;
            }
        }
        return true;
    }

    private static isVersioned(entry: string[] | VersionedDisabledIds): entry is VersionedDisabledIds {
        return !Array.isArray(entry);
    }

    /** Extracts the ID array from either the old (string[]) or new ({ version, ids }) format. */
    private static resolveIds(entry: string[] | VersionedDisabledIds | undefined): string[] {
        if (!entry) {
            return [];
        }
        return Array.isArray(entry) ? [...entry] : [...entry.ids];
    }

    /** Writes back an ID array, preserving the versioned wrapper if the original entry was versioned. */
    private static setIds(
        disabled: Record<string, string[] | VersionedDisabledIds>,
        fileName: string,
        original: string[] | VersionedDisabledIds,
        ids: string[],
    ): void {
        if (AutoContextConfigManager.isVersioned(original)) {
            disabled[fileName] = { version: original.version, ids };
        } else {
            disabled[fileName] = ids;
        }
    }

    /** Extracts "MAJOR.MINOR" from a full semver string. */
    private static majorMinor(version: string): string {
        const match = version.match(/^(\d+\.\d+)\./);
        return match ? match[1] : version;
    }
}
