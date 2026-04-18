import * as vscode from 'vscode';
import { writeFile, unlink, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { InstructionsParser } from './instructions-parser.js';
import type { AutoContextConfig, InstructionsFileConfig, McpToolConfig } from './types/autocontext-config.js';

const configFileName = '.autocontext.json';

export class AutoContextConfigManager implements vscode.Disposable {
    private readonly disposables: vscode.Disposable[] = [];
    private readonly didChangeEmitter = new vscode.EventEmitter<void>();
    private cachedConfig: AutoContextConfig | undefined;
    private hasLoggedNotFound = false;
    private writeQueue: Promise<void> = Promise.resolve();
    readonly onDidChange = this.didChangeEmitter.event;

    constructor(
        private readonly extensionPath: string,
        private readonly extensionVersion: string,
        private readonly outputChannel: vscode.OutputChannel,
    ) {
        const watcher = vscode.workspace.createFileSystemWatcher(`**/${configFileName}`);
        watcher.onDidChange(() => this.invalidate());
        watcher.onDidCreate(() => this.invalidate());
        watcher.onDidDelete(() => this.invalidate());
        this.disposables.push(watcher, this.didChangeEmitter);
    }

    private invalidate(): void {
        this.cachedConfig = undefined;
        this.hasLoggedNotFound = false;
        this.didChangeEmitter.fire();
    }

    readSync(): AutoContextConfig {
        return this.cachedConfig ?? {};
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
            this.cachedConfig = AutoContextConfigManager.fromDisk(parsed);
            return this.cachedConfig;
        } catch (err) {
            const isNotFound = err instanceof Error && 'code' in err && err.code === 'ENOENT';

            if (isNotFound && this.hasLoggedNotFound) {
                return {};
            }

            if (isNotFound) {
                this.hasLoggedNotFound = true;
            }

            this.outputChannel.appendLine(`[Config] Failed to read config: ${err instanceof Error ? err.message : err}`);

            return {};
        }
    }

    async getDisabledInstructions(fileName: string): Promise<ReadonlySet<string>> {
        const config = await this.read();
        return new Set(config.instructions?.[fileName]?.disabledInstructions ?? []);
    }

    async hasAnyDisabledInstructions(): Promise<boolean> {
        const config = await this.read();
        if (!config.instructions) {
            return false;
        }
        return Object.values(config.instructions).some(
            entry => (entry.disabledInstructions?.length ?? 0) > 0,
        );
    }

    async toggleInstruction(fileName: string, id: string, instructionVersion?: string): Promise<void> {
        return this.enqueue(async () => {
            const config = await this.read();
            config.instructions ??= {};

            const version = AutoContextConfigManager.majorMinor(instructionVersion ?? this.extensionVersion);
            const entry = config.instructions[fileName] ??= { version };

            const ids = entry.disabledInstructions ? [...entry.disabledInstructions] : [];
            const index = ids.indexOf(id);

            if (index >= 0) {
                ids.splice(index, 1);
            } else {
                ids.push(id);
            }

            if (ids.length > 0) {
                entry.disabledInstructions = ids;
                if (instructionVersion) {
                    entry.version = AutoContextConfigManager.majorMinor(instructionVersion);
                }
            } else {
                delete entry.disabledInstructions;
            }

            AutoContextConfigManager.pruneInstructionEntry(config, fileName);
            await this.writeConfig(config);
        });
    }

    async setMcpTools(tools: Record<string, McpToolConfig | false>): Promise<void> {
        return this.enqueue(async () => {
            const config = await this.read();
            const currentTools = config.mcpTools;

            if (AutoContextConfigManager.mcpToolsEqual(tools, currentTools)) {
                return;
            }

            if (Object.keys(tools).length === 0) {
                delete config.mcpTools;
            } else {
                config.mcpTools = tools;
            }

            await this.writeConfig(config);
        });
    }

    async resetInstructions(fileName: string): Promise<void> {
        return this.enqueue(async () => {
            const config = await this.read();
            if (!config.instructions?.[fileName]) {
                return;
            }

            const entry = config.instructions[fileName];
            delete entry.disabledInstructions;

            AutoContextConfigManager.pruneInstructionEntry(config, fileName);
            await this.writeConfig(config);
        });
    }

    async setInstructionEnabled(fileName: string, enabled: boolean): Promise<void> {
        return this.enqueue(async () => {
            const config = await this.read();
            config.instructions ??= {};

            if (enabled) {
                const entry = config.instructions[fileName];
                if (entry?.enabled === false) {
                    delete entry.enabled;
                    AutoContextConfigManager.pruneInstructionEntry(config, fileName);
                }
            } else {
                const existing = config.instructions[fileName] ?? {};
                config.instructions[fileName] = { ...existing, enabled: false };
            }

            await this.writeConfig(config);
        });
    }

    async setMcpToolEnabled(toolName: string, featureName: string | undefined, enabled: boolean): Promise<void> {
        return this.enqueue(async () => {
            const config = await this.read();
            config.mcpTools ??= {};

            if (!featureName) {
                // Leaf tool — enable or disable the whole tool entry.
                if (!enabled) {
                    config.mcpTools[toolName] = false;
                } else {
                    const current = config.mcpTools[toolName];
                    if (current === false) {
                        delete config.mcpTools[toolName];
                    } else if (current?.enabled === false) {
                        delete current.enabled;
                        if (Object.keys(current).length === 0) {
                            delete config.mcpTools[toolName];
                        }
                    }
                }
            } else {
                // Feature — add to or remove from disabledFeatures.
                if (!enabled) {
                    const current = config.mcpTools[toolName];
                    if (current !== false) {
                        const entry: McpToolConfig = current ?? {};
                        const arr = entry.disabledFeatures ?? [];
                        if (!arr.includes(featureName)) {
                            entry.disabledFeatures = [...arr, featureName];
                        }
                        config.mcpTools[toolName] = entry;
                    }
                } else {
                    const current = config.mcpTools[toolName];
                    if (current !== false && current !== undefined) {
                        const filtered = (current.disabledFeatures ?? []).filter(f => f !== featureName);
                        if (filtered.length > 0) {
                            current.disabledFeatures = filtered;
                        } else {
                            delete current.disabledFeatures;
                        }
                        if (Object.keys(current).length === 0) {
                            delete config.mcpTools[toolName];
                        }
                    }
                }
            }

            if (config.mcpTools && Object.keys(config.mcpTools).length === 0) {
                delete config.mcpTools;
            }

            await this.writeConfig(config);
        });
    }

    async removeOrphanedIds(): Promise<number> {
        return this.enqueue(async () => {
            const config = await this.read();
            if (!config.instructions) {
                return 0;
            }

            let removed = 0;

            await Promise.all(Object.entries(config.instructions).map(async ([fileName, entry]) => {
                const ids = entry.disabledInstructions;
                if (!ids || ids.length === 0) {
                    return;
                }

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
                        delete entry.disabledInstructions;
                    } else {
                        entry.disabledInstructions = filtered;
                    }
                } catch {
                    // File no longer exists — remove all its IDs.
                    removed += ids.length;
                    delete entry.disabledInstructions;
                }

                AutoContextConfigManager.pruneInstructionEntry(config, fileName);
            }));

            if (removed > 0) {
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
            if (!config.instructions) {
                return [];
            }

            const cleared: string[] = [];

            for (const [fileName, entry] of Object.entries(config.instructions)) {
                if (!entry.disabledInstructions || entry.disabledInstructions.length === 0) {
                    continue;
                }

                // No stored version — disabled IDs apply regardless of version.
                if (!entry.version) {
                    continue;
                }

                const catalogVersion = catalogVersions.get(fileName);
                if (!catalogVersion) {
                    continue;
                }

                const catalogMajorMinor = AutoContextConfigManager.majorMinor(catalogVersion);

                if (entry.version === catalogMajorMinor) {
                    continue;
                }

                // MAJOR or MINOR bumped — IDs may point to different rules.
                delete entry.disabledInstructions;
                AutoContextConfigManager.pruneInstructionEntry(config, fileName);
                cleared.push(fileName);
            }

            if (cleared.length > 0) {
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

        const isEmpty = !config.instructions && !config.diagnostic && !config.mcpTools;
        if (isEmpty) {
            try {
                await unlink(path);
            } catch {
                // File didn't exist — nothing to delete.
            }
            this.cachedConfig = {};
            this.didChangeEmitter.fire();
            return;
        }

        // Enforce deterministic key order: version first.
        const ordered: AutoContextConfig = { version: this.extensionVersion };
        if (config.diagnostic) ordered.diagnostic = config.diagnostic;
        if (config.instructions) ordered.instructions = config.instructions;
        if (config.mcpTools) ordered.mcpTools = config.mcpTools;

        await writeFile(path, JSON.stringify(AutoContextConfigManager.toDisk(ordered), null, 4) + '\n', 'utf-8');
        this.cachedConfig = ordered;
        this.didChangeEmitter.fire();
    }

    /** Removes an instruction entry if it has returned to the default state. */
    private static pruneInstructionEntry(config: AutoContextConfig, fileName: string): void {
        const entry = config.instructions?.[fileName];
        if (!entry) {
            return;
        }

        const hasState = entry.enabled === false
            || (entry.disabledInstructions !== undefined && entry.disabledInstructions.length > 0);

        if (!hasState) {
            delete config.instructions![fileName];
        }

        if (config.instructions && Object.keys(config.instructions).length === 0) {
            delete config.instructions;
        }
    }

    /** Converts on-disk JSON (kebab-case keys) to the internal camelCase model. */
    private static fromDisk(parsed: Record<string, unknown>): AutoContextConfig {
        const config: AutoContextConfig = {};

        if (typeof parsed.version === 'string') {
            config.version = parsed.version;
        }

        if (parsed.diagnostic) {
            config.diagnostic = parsed.diagnostic as AutoContextConfig['diagnostic'];
        }

        const rawInstructions = parsed.instructions as Record<string, Record<string, unknown>> | undefined;
        if (rawInstructions && typeof rawInstructions === 'object') {
            const instructions: Record<string, InstructionsFileConfig> = {};
            for (const [fileName, raw] of Object.entries(rawInstructions)) {
                const entry: InstructionsFileConfig = {};
                if (typeof raw.version === 'string') {
                    entry.version = raw.version;
                }
                if (raw.enabled === false) {
                    entry.enabled = false;
                }
                const disabledIds = raw['disabled-instructions'] as string[] | undefined;
                if (disabledIds && disabledIds.length > 0) {
                    entry.disabledInstructions = disabledIds;
                }
                instructions[fileName] = entry;
            }
            if (Object.keys(instructions).length > 0) {
                config.instructions = instructions;
            }
        }

        const rawTools = parsed['mcp-tools'] as Record<string, Record<string, unknown> | false> | undefined;
        if (rawTools && typeof rawTools === 'object') {
            const mcpTools: Record<string, McpToolConfig | false> = {};
            for (const [toolName, raw] of Object.entries(rawTools)) {
                if (raw === false) {
                    mcpTools[toolName] = false;
                } else {
                    const entry: McpToolConfig = {};
                    if (raw.enabled === false) {
                        entry.enabled = false;
                    }
                    if (typeof raw.version === 'string') {
                        entry.version = raw.version;
                    }
                    const disabledFeatures = raw['disabled-features'] as string[] | undefined;
                    if (disabledFeatures && disabledFeatures.length > 0) {
                        entry.disabledFeatures = disabledFeatures;
                    }
                    mcpTools[toolName] = entry;
                }
            }
            if (Object.keys(mcpTools).length > 0) {
                config.mcpTools = mcpTools;
            }
        }

        return config;
    }

    /** Converts the internal camelCase model to on-disk JSON (kebab-case keys). */
    private static toDisk(config: AutoContextConfig): Record<string, unknown> {
        const output: Record<string, unknown> = {};

        if (config.version) {
            output.version = config.version;
        }
        if (config.diagnostic) {
            output.diagnostic = config.diagnostic;
        }

        if (config.instructions) {
            const instructions: Record<string, Record<string, unknown>> = {};
            for (const [fileName, entry] of Object.entries(config.instructions)) {
                const raw: Record<string, unknown> = {};
                if (entry.version) {
                    raw.version = entry.version;
                }
                if (entry.enabled === false) {
                    raw.enabled = false;
                }
                if (entry.disabledInstructions && entry.disabledInstructions.length > 0) {
                    raw['disabled-instructions'] = entry.disabledInstructions;
                }
                instructions[fileName] = raw;
            }
            output.instructions = instructions;
        }

        if (config.mcpTools) {
            const tools: Record<string, Record<string, unknown> | false> = {};
            for (const [toolName, entry] of Object.entries(config.mcpTools)) {
                if (entry === false) {
                    tools[toolName] = false;
                } else {
                    const raw: Record<string, unknown> = {};
                    if (entry.enabled === false) {
                        raw.enabled = false;
                    }
                    if (entry.version) {
                        raw.version = entry.version;
                    }
                    if (entry.disabledFeatures && entry.disabledFeatures.length > 0) {
                        raw['disabled-features'] = entry.disabledFeatures;
                    }
                    tools[toolName] = raw;
                }
            }
            output['mcp-tools'] = tools;
        }

        return output;
    }

    private static mcpToolsEqual(
        a: Record<string, McpToolConfig | false>,
        b: Record<string, McpToolConfig | false> | undefined,
    ): boolean {
        if (!b) {
            return Object.keys(a).length === 0;
        }

        const keysA = Object.keys(a).sort();
        const keysB = Object.keys(b).sort();

        if (keysA.length !== keysB.length || keysA.some((k, i) => k !== keysB[i])) {
            return false;
        }

        for (const key of keysA) {
            const va = a[key];
            const vb = b[key];
            if (va === false || vb === false) {
                if (va !== vb) {
                    return false;
                }
                continue;
            }
            if (va.enabled !== vb.enabled
                || va.version !== vb.version
                || !AutoContextConfigManager.arraysEqual(va.disabledFeatures ?? [], vb.disabledFeatures ?? [])) {
                return false;
            }
        }

        return true;
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

    /** Extracts "MAJOR.MINOR" from a full semver string. */
    private static majorMinor(version: string): string {
        const match = version.match(/^(\d+\.\d+)\./);
        return match ? match[1] : version;
    }
}
