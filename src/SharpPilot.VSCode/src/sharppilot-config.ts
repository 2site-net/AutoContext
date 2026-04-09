import * as vscode from 'vscode';
import { readFileSync, writeFileSync, unlinkSync } from 'node:fs';
import { join } from 'node:path';
import { InstructionsParser } from './instructions-parser.js';

export interface SharpPilotConfig {
    version?: string;
    diagnostic?: {
        warnOnMissingId?: boolean;
    };
    instructions?: {
        disabled?: Record<string, string[]>;
    };
    mcpTools?: {
        disabled?: string[];
    };
}

const configFileName = '.sharppilot.json';

export class SharpPilotConfigManager implements vscode.Disposable {
    private readonly disposables: vscode.Disposable[] = [];
    private readonly didChangeEmitter = new vscode.EventEmitter<void>();
    private cachedConfig: SharpPilotConfig | undefined;
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

    read(): SharpPilotConfig {
        if (this.cachedConfig !== undefined) {
            return this.cachedConfig;
        }

        const path = this.configPath();
        if (!path) {
            return {};
        }

        try {
            const raw = readFileSync(path, 'utf-8');
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

    getDisabledInstructions(fileName: string): ReadonlySet<string> {
        const config = this.read();
        const ids = config.instructions?.disabled?.[fileName];
        return new Set(ids ?? []);
    }

    hasAnyDisabledInstructions(): boolean {
        const config = this.read();
        const disabled = config.instructions?.disabled;
        if (!disabled) {
            return false;
        }
        return Object.values(disabled).some(ids => ids.length > 0);
    }

    toggleInstruction(fileName: string, id: string): void {
        const config = this.read();
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

        this.writeConfig(config);
    }

    setDisabledTools(disabledTools: string[]): void {
        const config = this.read();
        const currentDisabled = config.mcpTools?.disabled ?? [];

        if (SharpPilotConfigManager.arraysEqual(disabledTools, currentDisabled)) {
            return;
        }

        if (disabledTools.length === 0) {
            delete config.mcpTools;
        } else {
            config.mcpTools = { disabled: disabledTools };
        }

        this.writeConfig(config);
    }

    resetInstructions(fileName: string): void {
        const config = this.read();
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

        this.writeConfig(config);
    }

    removeOrphanedIds(): number {
        const config = this.read();
        const disabled = config.instructions?.disabled;
        if (!disabled) {
            return 0;
        }

        let removed = 0;

        for (const [fileName, ids] of Object.entries(disabled)) {
            try {
                const filePath = join(this.extensionPath, 'instructions', fileName);
                const content = readFileSync(filePath, 'utf-8');
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
        }

        if (removed > 0) {
            // Clean up empty containers.
            if (Object.keys(disabled).length === 0) {
                delete config.instructions!.disabled;
            }
            if (Object.keys(config.instructions!).length === 0) {
                delete config.instructions;
            }

            this.writeConfig(config);
        }

        return removed;
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }

    private configPath(): string | undefined {
        const folder = vscode.workspace.workspaceFolders?.[0];
        if (!folder) {
            return undefined;
        }
        return join(folder.uri.fsPath, configFileName);
    }

    private writeConfig(config: SharpPilotConfig): void {
        const path = this.configPath();
        if (!path) {
            return;
        }

        this.cachedConfig = undefined;

        const isEmpty = !config.instructions && !config.diagnostic && !config.mcpTools;
        if (isEmpty) {
            try {
                unlinkSync(path);
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

        writeFileSync(path, JSON.stringify(output, null, 4) + '\n', 'utf-8');
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
