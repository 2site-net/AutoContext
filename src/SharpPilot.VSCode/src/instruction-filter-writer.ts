import * as vscode from 'vscode';
import { readFileSync, writeFileSync, mkdirSync, existsSync, readdirSync, rmSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { createHash } from 'node:crypto';
import { instructions, filteredContextKey } from './config.js';
import { parseInstructions } from './instruction-parser.js';
import type { SharpPilotConfigManager } from './sharppilot-config.js';

/**
 * Generates filtered instruction files that exclude disabled instructions.
 *
 * Multi-window safe via per-workspace staging + focus-based write:
 * - `instructions-filtered/.workspaces/<hash>/` holds pre-computed files per workspace
 * - `instructions-filtered/` root holds the live files that `chatInstructions` reads
 * - On activation and config change: compute → stage + promote (we own the window)
 * - On window focus: full write() (re-reads config, re-stages, promotes — caching makes
 *   this near-free when nothing changed, but catches missed watcher events)
 *
 * Per-file context keys (`sharp-pilot.filtered.<suffix>`) tell `package.json` which
 * entry to activate — original or filtered — so Copilot always reads the correct file.
 */
export class InstructionFilterWriter implements vscode.Disposable {
    private readonly filteredRoot: string;
    private stagingDir: string;
    private readonly disposables: vscode.Disposable[] = [];
    private debounceTimer: ReturnType<typeof setTimeout> | undefined;

    constructor(
        private readonly extensionPath: string,
        private readonly configManager: SharpPilotConfigManager,
    ) {
        this.filteredRoot = join(extensionPath, 'instructions-filtered');
        this.stagingDir = join(this.filteredRoot, '.workspaces', workspaceHash());
        this.disposables.push(
            configManager.onDidChange(() => this.scheduleWrite()),
            vscode.workspace.onDidChangeWorkspaceFolders(() => {
                this.stagingDir = join(this.filteredRoot, '.workspaces', workspaceHash());
                this.write();
            }),
        );
    }

    private scheduleWrite(): void {
        if (this.debounceTimer !== undefined) {
            clearTimeout(this.debounceTimer);
        }
        this.debounceTimer = setTimeout(() => {
            this.debounceTimer = undefined;
            this.write();
        }, 250);
    }

    /** Compute filtered files → write to staging → promote to live root. */
    write(): void {
        mkdirSync(this.stagingDir, { recursive: true });

        const config = this.configManager.read();
        const disabledInstructionsMap = config.instructions?.disabledInstructions ?? {};

        for (const entry of instructions) {
            const disabledIds = disabledInstructionsMap[entry.fileName];
            const hasDisabled = disabledIds !== undefined && disabledIds.length > 0;

            if (hasDisabled) {
                this.writeFiltered(entry.fileName, new Set(disabledIds));
            } else {
                this.stageOriginal(entry.fileName);
            }

            vscode.commands.executeCommand(
                'setContext',
                filteredContextKey(entry.settingId),
                hasDisabled,
            );
        }

        this.promote();
    }

    /** Copy staged files → live root. Called by write() after staging. */
    private promote(): void {
        mkdirSync(this.filteredRoot, { recursive: true });

        for (const entry of instructions) {
            const staged = join(this.stagingDir, entry.fileName);
            const live = join(this.filteredRoot, entry.fileName);
            copyIfChanged(staged, live);
        }
    }

    removeOrphanedStagingDirs(): void {
        const workspacesDir = join(this.filteredRoot, '.workspaces');
        if (!existsSync(workspacesDir)) {
            return;
        }

        const currentHash = workspaceHash();
        const oneHourAgo = Date.now() - 60 * 60 * 1000;
        let entries: string[];
        try {
            entries = readdirSync(workspacesDir);
        } catch {
            return;
        }

        for (const entry of entries) {
            if (entry === currentHash) {
                continue;
            }

            const dirPath = join(workspacesDir, entry);
            try {
                const stat = statSync(dirPath);
                if (stat.mtimeMs > oneHourAgo) {
                    continue;
                }
                rmSync(dirPath, { recursive: true });
            } catch {
                // Permission error or in-use — skip.
            }
        }
    }

    dispose(): void {
        if (this.debounceTimer !== undefined) {
            clearTimeout(this.debounceTimer);
        }
        for (const d of this.disposables) {
            d.dispose();
        }
    }

    private stageOriginal(fileName: string): void {
        const src = join(this.extensionPath, 'instructions', fileName);
        const dest = join(this.stagingDir, fileName);

        try {
            const content = readFileSync(src, 'utf-8');
            writeIfChanged(dest, content);
        } catch {
            // Source file missing — skip.
        }
    }

    private writeFiltered(fileName: string, disabledIds: ReadonlySet<string>): void {
        const src = join(this.extensionPath, 'instructions', fileName);
        const dest = join(this.stagingDir, fileName);

        let content: string;
        try {
            content = readFileSync(src, 'utf-8');
        } catch {
            return;
        }

        const { instructions: parsedInstructions } = parseInstructions(content);
        const lines = content.split('\n');
        const linesToRemove = new Set<number>();

        for (const instruction of parsedInstructions) {
            if (instruction.id !== undefined && disabledIds.has(instruction.id)) {
                for (let i = instruction.startLine; i <= instruction.endLine; i++) {
                    linesToRemove.add(i);
                }
            }
        }

        const filtered = lines.filter((_, i) => !linesToRemove.has(i));
        writeIfChanged(dest, filtered.join('\n'));
    }
}

function workspaceHash(): string {
    const root = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath ?? 'no-workspace';
    return createHash('sha256').update(root).digest('hex').slice(0, 12);
}

function writeIfChanged(dest: string, content: string): void {
    if (existsSync(dest)) {
        try {
            if (readFileSync(dest, 'utf-8') === content) {
                return;
            }
        } catch {
            // Read failed — fall through to write.
        }
    }

    writeFileSync(dest, content, 'utf-8');
}

function copyIfChanged(src: string, dest: string): void {
    if (!existsSync(src)) {
        return;
    }

    try {
        const content = readFileSync(src, 'utf-8');
        writeIfChanged(dest, content);
    } catch {
        // Source read failed — skip.
    }
}
