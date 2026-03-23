import * as vscode from 'vscode';
import { readFileSync, writeFileSync, unlinkSync } from 'node:fs';
import { join } from 'node:path';
import { parseRules } from './rule-parser.js';

export interface SharpPilotConfig {
    version?: string;
    instructions?: {
        disabledRules?: Record<string, string[]>;
    };
}

const configFileName = '.sharppilot.json';

export class SharpPilotConfigManager implements vscode.Disposable {
    private readonly disposables: vscode.Disposable[] = [];
    private readonly didChangeEmitter = new vscode.EventEmitter<void>();
    readonly onDidChange = this.didChangeEmitter.event;

    constructor(
        private readonly extensionPath: string,
        private readonly extensionVersion: string,
    ) {
        const watcher = vscode.workspace.createFileSystemWatcher(`**/${configFileName}`);
        watcher.onDidChange(() => this.didChangeEmitter.fire());
        watcher.onDidCreate(() => this.didChangeEmitter.fire());
        watcher.onDidDelete(() => this.didChangeEmitter.fire());
        this.disposables.push(watcher, this.didChangeEmitter);
    }

    read(): SharpPilotConfig {
        const path = this.configPath();
        if (!path) {
            return {};
        }

        try {
            const raw = readFileSync(path, 'utf-8');
            return JSON.parse(raw) as SharpPilotConfig;
        } catch {
            return {};
        }
    }

    getDisabledRules(fileName: string): ReadonlySet<string> {
        const config = this.read();
        const hashes = config.instructions?.disabledRules?.[fileName];
        return new Set(hashes ?? []);
    }

    hasAnyDisabledRules(): boolean {
        const config = this.read();
        const disabledRules = config.instructions?.disabledRules;
        if (!disabledRules) {
            return false;
        }
        return Object.values(disabledRules).some(hashes => hashes.length > 0);
    }

    toggleRule(fileName: string, hash: string): void {
        const config = this.read();
        config.instructions ??= {};
        config.instructions.disabledRules ??= {};

        const hashes = config.instructions.disabledRules[fileName] ?? [];
        const index = hashes.indexOf(hash);

        if (index >= 0) {
            hashes.splice(index, 1);
        } else {
            hashes.push(hash);
        }

        if (hashes.length === 0) {
            delete config.instructions.disabledRules[fileName];
        } else {
            config.instructions.disabledRules[fileName] = hashes;
        }

        // Clean up empty containers.
        if (Object.keys(config.instructions.disabledRules).length === 0) {
            delete config.instructions.disabledRules;
        }
        if (Object.keys(config.instructions).length === 0) {
            delete config.instructions;
        }

        this.write(config);
    }

    resetRules(fileName: string): void {
        const config = this.read();
        const disabledRules = config.instructions?.disabledRules;
        if (!disabledRules?.[fileName]) {
            return;
        }

        delete disabledRules[fileName];

        if (Object.keys(disabledRules).length === 0) {
            delete config.instructions!.disabledRules;
        }
        if (Object.keys(config.instructions!).length === 0) {
            delete config.instructions;
        }

        this.write(config);
    }

    removeOrphanedHashes(): number {
        const config = this.read();
        const disabledRules = config.instructions?.disabledRules;
        if (!disabledRules) {
            return 0;
        }

        let removed = 0;

        for (const [fileName, hashes] of Object.entries(disabledRules)) {
            try {
                const filePath = join(this.extensionPath, 'instructions', fileName);
                const content = readFileSync(filePath, 'utf-8');
                const rules = parseRules(content);
                const validHashes = new Set(rules.map(r => r.hash));

                const filtered = hashes.filter(h => validHashes.has(h));
                removed += hashes.length - filtered.length;

                if (filtered.length === 0) {
                    delete disabledRules[fileName];
                } else {
                    disabledRules[fileName] = filtered;
                }
            } catch {
                // File no longer exists — remove all its hashes.
                removed += hashes.length;
                delete disabledRules[fileName];
            }
        }

        if (removed > 0) {
            // Clean up empty containers.
            if (Object.keys(disabledRules).length === 0) {
                delete config.instructions!.disabledRules;
            }
            if (Object.keys(config.instructions!).length === 0) {
                delete config.instructions;
            }

            this.write(config);
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

    private write(config: SharpPilotConfig): void {
        const path = this.configPath();
        if (!path) {
            return;
        }

        const isEmpty = !config.instructions;
        if (isEmpty) {
            try {
                unlinkSync(path);
            } catch {
                // File didn't exist — nothing to delete.
            }
            return;
        }

        config.version = this.extensionVersion;
        writeFileSync(path, JSON.stringify(config, null, 4) + '\n', 'utf-8');
    }
}
