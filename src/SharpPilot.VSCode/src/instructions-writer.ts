import * as vscode from 'vscode';
import { readFileSync, writeFileSync, mkdirSync, existsSync, readdirSync, rmSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { createHash } from 'node:crypto';
import { instructions } from './instructions-catalog.js';
import { InstructionsParser } from './instructions-parser.js';
import type { SharpPilotConfigManager } from './sharppilot-config.js';

/**
 * Generates normalized instruction files with disabled instructions removed
 * and `[INSTxxxx]` tags stripped.
 *
 * Multi-window safe via per-workspace staging + focus-based write:
 * - `instructions/.workspaces/<hash>/` holds pre-computed files per workspace
 * - `instructions/.generated/` holds the live files that `chatInstructions` reads
 * - On activation and config change: compute → stage + promote (we own the window)
 * - On window focus: full write() (re-reads config, re-stages, promotes — caching makes
 *   this near-free when nothing changed, but catches missed watcher events)
 */
export class InstructionsWriter implements vscode.Disposable {
    private readonly generatedRoot: string;
    private stagingDir: string;
    private readonly disposables: vscode.Disposable[] = [];
    private debounceTimer: ReturnType<typeof setTimeout> | undefined;

    constructor(
        private readonly extensionPath: string,
        private readonly configManager: SharpPilotConfigManager,
    ) {
        this.generatedRoot = join(extensionPath, 'instructions', '.generated');
        this.stagingDir = join(extensionPath, 'instructions', '.workspaces', InstructionsWriter.workspaceHash());
        this.disposables.push(
            configManager.onDidChange(() => this.scheduleWrite()),
            vscode.workspace.onDidChangeWorkspaceFolders(() => {
                this.stagingDir = join(this.extensionPath, 'instructions', '.workspaces', InstructionsWriter.workspaceHash());
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

    /** Compute normalized files → write to staging → promote to live root. */
    write(): void {
        mkdirSync(this.stagingDir, { recursive: true });

        const config = this.configManager.read();
        const disabledInstructionsMap = config.instructions?.disabledInstructions ?? {};

        for (const entry of instructions) {
            const disabledIds = disabledInstructionsMap[entry.fileName];
            const disabled = disabledIds !== undefined && disabledIds.length > 0
                ? new Set(disabledIds)
                : undefined;
            this.writeNormalized(entry.fileName, disabled);
        }

        this.promote();
    }

    /** Copy staged files → live root. */
    private promote(): void {
        mkdirSync(this.generatedRoot, { recursive: true });

        for (const entry of instructions) {
            const staged = join(this.stagingDir, entry.fileName);
            const live = join(this.generatedRoot, entry.fileName);
            InstructionsWriter.copyIfChanged(staged, live);
        }
    }

    removeOrphanedStagingDirs(): void {
        const workspacesDir = join(this.extensionPath, 'instructions', '.workspaces');
        if (!existsSync(workspacesDir)) {
            return;
        }

        const currentHash = InstructionsWriter.workspaceHash();
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

    private writeNormalized(fileName: string, disabledIds: ReadonlySet<string> | undefined): void {
        const src = join(this.extensionPath, 'instructions', fileName);
        const dest = join(this.stagingDir, fileName);

        let content: string;
        try {
            content = readFileSync(src, 'utf-8');
        } catch {
            return;
        }

        if (disabledIds !== undefined && disabledIds.size > 0) {
            const { instructions: parsedInstructions } = InstructionsParser.parse(content);
            const lines = content.split('\n');
            const linesToRemove = new Set<number>();

            for (const instruction of parsedInstructions) {
                if (instruction.id !== undefined && disabledIds.has(instruction.id)) {
                    for (let i = instruction.startLine; i <= instruction.endLine; i++) {
                        linesToRemove.add(i);
                    }
                }
            }

            content = lines.filter((_, i) => !linesToRemove.has(i)).join('\n');
        }

        content = InstructionsWriter.stripInstructionIds(content);
        InstructionsWriter.writeIfChanged(dest, content);
    }

    private static readonly instructionIdTag = /\[INST\d{4}\]\s*/g;

    private static stripInstructionIds(content: string): string {
        return content.replace(InstructionsWriter.instructionIdTag, '');
    }

    private static workspaceHash(): string {
        const root = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath ?? 'no-workspace';
        return createHash('sha256').update(root).digest('hex').slice(0, 12);
    }

    private static writeIfChanged(dest: string, content: string): void {
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

    private static copyIfChanged(src: string, dest: string): void {
        if (!existsSync(src)) {
            return;
        }

        try {
            const content = readFileSync(src, 'utf-8');
            InstructionsWriter.writeIfChanged(dest, content);
        } catch {
            // Source read failed — skip.
        }
    }
}
