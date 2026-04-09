import * as vscode from 'vscode';
import { writeFile, unlink, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { InstructionsParser } from './instructions-parser.js';
import type { SharpPilotConfig } from './types/sharppilot-config.js';

const configFileName = '.sharppilot.json';

export class SharpPilotConfigManager implements vscode.Disposable {
    private readonly disposables: vscode.Disposable[] = [];
    private readonly didChangeEmitter = new vscode.EventEmitter<void>();
    private cachedConfig: SharpPilotConfig | undefined;
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

    async read(): Promise<SharpPilotConfig> {
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

            this.cachedConfig = parsed as SharpPilotConfig;
            return this.cachedConfig;
        } catch {
            return {};
        }
    }

    async getDisabledInstructions(fileName: string): Promise<ReadonlySet<string>> {
        const config = await this.read();
        const ids = config.instructions?.disabled?.[fileName];
        return new Set(ids ?? []);
    }

    async hasAnyDisabledInstructions(): Promise<boolean> {
        const config = await this.read();
        const disabled = config.instructions?.disabled;
        if (!disabled) {
            return false;
        }
        return Object.values(disabled).some(ids => ids.length > 0);
    }

    async toggleInstruction(fileName: string, id: string): Promise<void> {
        return this.enqueue(async () => {
            const config = await this.read();
            config.instructions ??= {};
            config.instructions.disabled ??= {};

            const ids = config.instructions.disabled[fileName] ?? [];
            const index = ids.indexOf(id);

            if (index >= 0) {
                ids.splice(index, 1);
            } else {
                ids.push(id);
            }

            if (ids.length === 0) {
                delete config.instructions.disabled[fileName];
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

            if (SharpPilotConfigManager.arraysEqual(disabledTools, currentDisabled)) {
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

            await Promise.all(Object.entries(disabled).map(async ([fileName, ids]) => {
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
                        disabled[fileName] = filtered;
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

    private async writeConfig(config: SharpPilotConfig): Promise<void> {
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
        const ordered: SharpPilotConfig = { version: this.extensionVersion };
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
}
