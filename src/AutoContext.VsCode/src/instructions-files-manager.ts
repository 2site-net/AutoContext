import * as vscode from 'vscode';
import { readFile, writeFile, mkdir, readdir, stat, rm, access } from 'node:fs/promises';
import { join } from 'node:path';
import { createHash } from 'node:crypto';
import type { InstructionsFilesManifest } from './instructions-files-manifest.js';
import { InstructionsFileParser } from './instructions-file-parser.js';
import type { AutoContextConfigManager } from './autocontext-config.js';
import type { Logger } from './types/logger.js';

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
export class InstructionsFilesManager implements vscode.Disposable {
    private readonly generatedRoot: string;
    private stagingDir: string;
    private readonly disposables: vscode.Disposable[] = [];
    private debounceTimer: ReturnType<typeof setTimeout> | undefined;
    private writeInFlight: Promise<void> | undefined;

    constructor(
        private readonly extensionPath: string,
        private readonly configManager: AutoContextConfigManager,
        private readonly manifest: InstructionsFilesManifest,
        private readonly logger: Logger,
    ) {
        this.generatedRoot = join(extensionPath, 'instructions', '.generated');
        this.stagingDir = join(extensionPath, 'instructions', '.workspaces', InstructionsFilesManager.workspaceHash());
        this.disposables.push(
            configManager.onDidChange(() => this.scheduleWrite()),
            vscode.workspace.onDidChangeWorkspaceFolders(() => {
                this.stagingDir = join(this.extensionPath, 'instructions', '.workspaces', InstructionsFilesManager.workspaceHash());
                void this.write().catch(err =>
                    this.logger.error('Failed to write on workspace change', err),
                );
            }),
        );
    }

    private scheduleWrite(): void {
        if (this.debounceTimer !== undefined) {
            clearTimeout(this.debounceTimer);
        }
        this.debounceTimer = setTimeout(() => {
            this.debounceTimer = undefined;
            void this.write().catch(err =>
                this.logger.error('Failed to write on config change', err),
            );
        }, 250);
    }

    /** Compute normalized files → write to staging → promote to live root. */
    async write(): Promise<void> {
        if (this.writeInFlight) {
            await this.writeInFlight;
        }

        const task = this.doWrite();
        this.writeInFlight = task;
        try {
            await task;
        } finally {
            if (this.writeInFlight === task) {
                this.writeInFlight = undefined;
            }
        }
    }

    private async doWrite(): Promise<void> {
        await mkdir(this.stagingDir, { recursive: true });

        const config = await this.configManager.read();

        await Promise.all(this.manifest.instructions.map(entry => {
            const disabledIds = config.instructions?.[entry.name]?.disabledInstructions;
            const disabled = disabledIds && disabledIds.length > 0
                ? new Set(disabledIds)
                : undefined;
            return this.writeNormalized(entry.name, disabled);
        }));

        await this.promote();
    }

    /** Copy staged files → live root. */
    private async promote(): Promise<void> {
        await mkdir(this.generatedRoot, { recursive: true });

        await Promise.all(this.manifest.instructions.map(entry => {
            const staged = join(this.stagingDir, entry.name);
            const live = join(this.generatedRoot, entry.name);
            return InstructionsFilesManager.copyIfChanged(staged, live);
        }));
    }

    async removeOrphanedStagingDirs(): Promise<void> {
        const workspacesDir = join(this.extensionPath, 'instructions', '.workspaces');
        try {
            await access(workspacesDir);
        } catch {
            return;
        }

        const currentHash = InstructionsFilesManager.workspaceHash();
        const oneHourAgo = Date.now() - 60 * 60 * 1000;
        let entries: string[];
        try {
            entries = await readdir(workspacesDir);
        } catch {
            return;
        }

        await Promise.all(entries.map(async entry => {
            if (entry === currentHash) {
                return;
            }

            const dirPath = join(workspacesDir, entry);
            try {
                const s = await stat(dirPath);
                if (s.mtimeMs > oneHourAgo) {
                    return;
                }
                await rm(dirPath, { recursive: true });
            } catch {
                // Permission error or in-use — skip.
            }
        }));
    }

    dispose(): void {
        if (this.debounceTimer !== undefined) {
            clearTimeout(this.debounceTimer);
        }
        for (const d of this.disposables) {
            d.dispose();
        }
    }

    private async writeNormalized(fileName: string, disabledIds: ReadonlySet<string> | undefined): Promise<void> {
        const src = join(this.extensionPath, 'instructions', fileName);
        const dest = join(this.stagingDir, fileName);

        let content: string;
        let parsedResult;
        try {
            ({ content, result: parsedResult } = await InstructionsFileParser.fromFile(src));
        } catch (err) {
            this.logger.error(`Failed to parse ${fileName}`, err);
            return;
        }

        if (disabledIds !== undefined && disabledIds.size > 0) {
            const { instructions: parsedInstructions } = parsedResult;
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

        content = InstructionsFilesManager.stripInstructionIds(content);
        await InstructionsFilesManager.writeIfChanged(dest, content);
    }

    private static readonly instructionIdTag = /\[INST\d{4}\]\s*/g;

    private static stripInstructionIds(content: string): string {
        return content.replace(InstructionsFilesManager.instructionIdTag, '');
    }

    private static workspaceHash(): string {
        const root = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath ?? 'no-workspace';
        return createHash('sha256').update(root).digest('hex').slice(0, 12);
    }

    private static async writeIfChanged(dest: string, content: string): Promise<void> {
        try {
            const existing = await readFile(dest, 'utf-8');
            if (existing === content) {
                return;
            }
        } catch {
            // File doesn't exist or read failed — fall through to write.
        }

        await writeFile(dest, content, 'utf-8');
    }

    private static async copyIfChanged(src: string, dest: string): Promise<void> {
        try {
            const content = await readFile(src, 'utf-8');
            await InstructionsFilesManager.writeIfChanged(dest, content);
        } catch {
            // Source read failed — skip.
        }
    }

}
