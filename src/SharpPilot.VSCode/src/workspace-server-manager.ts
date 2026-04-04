import * as vscode from 'vscode';
import { join } from 'node:path';
import { spawn, type ChildProcess } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { createInterface } from 'node:readline';

/**
 * Manages the lifecycle of the `SharpPilot.WorkspaceServer` service process.
 * Spawns the service on activation and kills it on disposal.
 */
export class WorkspaceServerManager implements vscode.Disposable {
    private readonly ext = process.platform === 'win32' ? '.exe' : '';
    private readonly pipeName = `sharppilot-workspace-${randomUUID().replace(/-/g, '').slice(0, 12)}`;
    private process: ChildProcess | undefined;
    private ready = false;

    constructor(
        private readonly extensionPath: string,
        private readonly outputChannel: vscode.OutputChannel,
        private readonly workspaceRoot: string | undefined,
    ) {}

    /**
     * The pipe name the service is listening on.
     * Returns `undefined` if the service has not been started.
     */
    getPipeName(): string | undefined {
        return this.ready ? this.pipeName : undefined;
    }

    /**
     * Spawns the workspace service process.
     */
    start(): void {
        const command = join(this.extensionPath, 'mcp', 'SharpPilot.WorkspaceServer', `SharpPilot.WorkspaceServer${this.ext}`);

        const args = ['--pipe', this.pipeName];

        if (this.workspaceRoot) {
            args.push('--workspace-root', this.workspaceRoot);
        }

        this.process = spawn(command, args, {
            stdio: ['ignore', 'pipe', 'pipe'],
        });

        // Read the first line of stdout for the ready signal
        if (this.process.stdout) {
            const rl = createInterface({ input: this.process.stdout });

            rl.once('line', (line: string) => {
                try {
                    const msg = JSON.parse(line) as { pipe?: string };

                    if (msg.pipe) {
                        this.ready = true;
                        this.outputChannel.appendLine(`[WorkspaceServer] Service ready on pipe: ${msg.pipe}`);
                    }
                } catch {
                    this.outputChannel.appendLine(`[WorkspaceServer] Unexpected ready message: ${line}`);
                }

                rl.close();
            });
        }

        if (this.process.stderr) {
            const rl = createInterface({ input: this.process.stderr });

            rl.on('line', (line: string) => {
                this.outputChannel.appendLine(`[WorkspaceServer] ${line}`);
            });
        }

        this.process.on('error', (err) => {
            this.ready = false;
            this.outputChannel.appendLine(`[WorkspaceServer] Failed to start: ${err.message}`);
        });

        this.process.on('exit', (code) => {
            this.ready = false;
            this.outputChannel.appendLine(`[WorkspaceServer] Service exited with code ${code}`);
        });
    }

    dispose(): void {
        if (this.process) {
            this.process.kill();
            this.process = undefined;
            this.ready = false;
        }
    }
}
