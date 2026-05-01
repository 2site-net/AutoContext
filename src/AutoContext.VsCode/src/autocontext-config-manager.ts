import * as vscode from 'vscode';
import { writeFile, unlink, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { InstructionsFileParser } from './instructions-file-parser.js';
import { AutoContextConfig } from './autocontext-config.js';
import type { InstructionsFileConfigEntry } from '#types/instructions-file-config-entry.js';
import type { McpToolConfigEntry } from '#types/mcp-tool-config-entry.js';
import type { Logger } from '#types/logger.js';

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
        private readonly logger: Logger,
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
        return this.cachedConfig ?? new AutoContextConfig();
    }

    async read(): Promise<AutoContextConfig> {
        if (this.cachedConfig !== undefined) {
            return this.cachedConfig;
        }

        const path = this.configPath();
        if (!path) {
            return new AutoContextConfig();
        }

        try {
            const raw = await readFile(path, 'utf-8');
            const parsed: Record<string, unknown> = JSON.parse(raw);
            this.cachedConfig = AutoContextConfigManager.fromDisk(parsed);
            return this.cachedConfig;
        } catch (err) {
            const isNotFound = err instanceof Error && 'code' in err && err.code === 'ENOENT';

            if (isNotFound && this.hasLoggedNotFound) {
                return new AutoContextConfig();
            }

            if (isNotFound) {
                this.hasLoggedNotFound = true;
            }

            this.logger.error('Failed to read config', err);

            return new AutoContextConfig();
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

    async setMcpTools(tools: Record<string, McpToolConfigEntry | false>): Promise<void> {
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

    async setMcpToolEnabled(toolName: string, taskName: string | undefined, enabled: boolean): Promise<void> {
        return this.enqueue(async () => {
            const config = await this.read();
            config.mcpTools ??= {};

            if (!taskName) {
                // Leaf tool — enable or disable the whole tool entry.
                if (!enabled) {
                    const current = config.mcpTools[toolName];
                    if (current !== false && current !== undefined && Object.keys(current).length > 0) {
                        // Preserve existing config (e.g. disabledTasks) and mark disabled.
                        current.enabled = false;
                    } else {
                        config.mcpTools[toolName] = false;
                    }
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
                // Task — add to or remove from disabledTasks.
                if (!enabled) {
                    let current = config.mcpTools[toolName];
                    // Upgrade shorthand `false` to an object so we can store disabledTasks.
                    if (current === false) {
                        current = { enabled: false };
                        config.mcpTools[toolName] = current;
                    }
                    const entry: McpToolConfigEntry = current ?? {};
                    const arr = entry.disabledTasks ?? [];
                    if (!arr.includes(taskName)) {
                        entry.disabledTasks = [...arr, taskName];
                    }
                    config.mcpTools[toolName] = entry;
                } else {
                    let current = config.mcpTools[toolName];
                    // Upgrade shorthand `false` to an object so we can modify disabledTasks.
                    if (current === false) {
                        current = { enabled: false };
                        config.mcpTools[toolName] = current;
                    }
                    if (current !== undefined) {
                        const filtered = (current.disabledTasks ?? []).filter(f => f !== taskName);
                        if (filtered.length > 0) {
                            current.disabledTasks = filtered;
                        } else {
                            delete current.disabledTasks;
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
                    const { instructions } = InstructionsFileParser.parse(content);
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
                } catch (err) {
                    // File no longer exists — remove all its IDs.
                    this.logger.warn(`Instruction file '${fileName}' is missing; clearing ${ids.length} disabled id(s) from config`, err);
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
            } catch (err) {
                // File didn't exist — nothing to delete.
                this.logger.debug(`No config file to delete: ${path}`, err);
            }
            this.cachedConfig = new AutoContextConfig();
            this.didChangeEmitter.fire();
            return;
        }

        // Enforce deterministic key order: version first.
        const ordered = new AutoContextConfig({ version: this.extensionVersion });
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

    /** Converts on-disk JSON to the internal model. */
    private static fromDisk(parsed: Record<string, unknown>): AutoContextConfig {
        const config = new AutoContextConfig();

        if (typeof parsed.version === 'string') {
            config.version = parsed.version;
        }

        if (parsed.diagnostic) {
            config.diagnostic = parsed.diagnostic as AutoContextConfig['diagnostic'];
        }

        const rawInstructions = parsed.instructions as Record<string, Record<string, unknown>> | undefined;
        if (rawInstructions && typeof rawInstructions === 'object') {
            const instructions: Record<string, InstructionsFileConfigEntry> = {};
            for (const [fileName, raw] of Object.entries(rawInstructions)) {
                const entry: InstructionsFileConfigEntry = {};
                if (typeof raw.version === 'string') {
                    entry.version = raw.version;
                }
                if (raw.enabled === false) {
                    entry.enabled = false;
                }
                const disabledIds = raw.disabledInstructions as string[] | undefined;
                if (disabledIds && disabledIds.length > 0) {
                    entry.disabledInstructions = disabledIds;
                }
                instructions[fileName] = entry;
            }
            if (Object.keys(instructions).length > 0) {
                config.instructions = instructions;
            }
        }

        const rawTools = parsed.mcpTools as Record<string, Record<string, unknown> | false> | undefined;
        if (rawTools && typeof rawTools === 'object') {
            const mcpTools: Record<string, McpToolConfigEntry | false> = {};
            for (const [toolName, raw] of Object.entries(rawTools)) {
                if (raw === false) {
                    mcpTools[toolName] = false;
                } else {
                    const entry: McpToolConfigEntry = {};
                    if (raw.enabled === false) {
                        entry.enabled = false;
                    }
                    if (typeof raw.version === 'string') {
                        entry.version = raw.version;
                    }
                    const disabledTasks = raw.disabledTasks as string[] | undefined;
                    if (disabledTasks && disabledTasks.length > 0) {
                        entry.disabledTasks = disabledTasks;
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

    /** Converts the internal model to on-disk JSON. Keys are camelCase. */
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
                    raw.disabledInstructions = entry.disabledInstructions;
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
                    if (entry.disabledTasks && entry.disabledTasks.length > 0) {
                        raw.disabledTasks = entry.disabledTasks;
                    }
                    tools[toolName] = raw;
                }
            }
            output.mcpTools = tools;
        }

        return output;
    }

    private static mcpToolsEqual(
        a: Record<string, McpToolConfigEntry | false>,
        b: Record<string, McpToolConfigEntry | false> | undefined,
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
                || !AutoContextConfigManager.arraysEqual(va.disabledTasks ?? [], vb.disabledTasks ?? [])) {
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
