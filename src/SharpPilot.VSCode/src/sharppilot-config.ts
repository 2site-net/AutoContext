import * as vscode from 'vscode';
import { readFileSync, writeFileSync, unlinkSync } from 'node:fs';
import { join } from 'node:path';
import { parseInstructions } from './instruction-parser.js';

export interface SharpPilotConfig {
    version?: string;
    instructions?: {
        disabledInstructions?: Record<string, string[]>;
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

    getDisabledInstructions(fileName: string): ReadonlySet<string> {
        const config = this.read();
        const hashes = config.instructions?.disabledInstructions?.[fileName];
        return new Set(hashes ?? []);
    }

    hasAnyDisabledInstructions(): boolean {
        const config = this.read();
        const disabledInstructions = config.instructions?.disabledInstructions;
        if (!disabledInstructions) {
            return false;
        }
        return Object.values(disabledInstructions).some(hashes => hashes.length > 0);
    }

    toggleInstruction(fileName: string, hash: string): void {
        const config = this.read();
        config.instructions ??= {};
        config.instructions.disabledInstructions ??= {};

        const hashes = config.instructions.disabledInstructions[fileName] ?? [];
        const index = hashes.indexOf(hash);

        if (index >= 0) {
            hashes.splice(index, 1);
        } else {
            hashes.push(hash);
        }

        if (hashes.length === 0) {
            delete config.instructions.disabledInstructions[fileName];
        } else {
            config.instructions.disabledInstructions[fileName] = hashes;
        }

        // Clean up empty containers.
        if (Object.keys(config.instructions.disabledInstructions).length === 0) {
            delete config.instructions.disabledInstructions;
        }
        if (Object.keys(config.instructions).length === 0) {
            delete config.instructions;
        }

        this.write(config);
    }

    resetInstructions(fileName: string): void {
        const config = this.read();
        const disabledInstructions = config.instructions?.disabledInstructions;
        if (!disabledInstructions?.[fileName]) {
            return;
        }

        delete disabledInstructions[fileName];

        if (Object.keys(disabledInstructions).length === 0) {
            delete config.instructions!.disabledInstructions;
        }
        if (Object.keys(config.instructions!).length === 0) {
            delete config.instructions;
        }

        this.write(config);
    }

    removeOrphanedHashes(): number {
        const config = this.read();
        const disabledInstructions = config.instructions?.disabledInstructions;
        if (!disabledInstructions) {
            return 0;
        }

        let removed = 0;

        for (const [fileName, hashes] of Object.entries(disabledInstructions)) {
            try {
                const filePath = join(this.extensionPath, 'instructions', fileName);
                const content = readFileSync(filePath, 'utf-8');
                const parsedInstructions = parseInstructions(content);
                const validHashes = new Set(parsedInstructions.map(r => r.hash));

                const filtered = hashes.filter(h => validHashes.has(h));
                removed += hashes.length - filtered.length;

                if (filtered.length === 0) {
                    delete disabledInstructions[fileName];
                } else {
                    disabledInstructions[fileName] = filtered;
                }
            } catch {
                // File no longer exists — remove all its hashes.
                removed += hashes.length;
                delete disabledInstructions[fileName];
            }
        }

        if (removed > 0) {
            // Clean up empty containers.
            if (Object.keys(disabledInstructions).length === 0) {
                delete config.instructions!.disabledInstructions;
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
