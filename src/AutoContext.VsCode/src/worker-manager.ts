import * as vscode from 'vscode';
import { join } from 'node:path';
import { spawn, type ChildProcess } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { createInterface } from 'node:readline';
import { formatEndpoint } from './endpoint-formatter.js';
import type { ServersManifest } from './types/servers-manifest.js';

/**
 * A worker the extension spawns and keeps alive for the lifetime of
 * the VS Code window.
 */
interface WorkerSpec {
    /** Short identity used as the output-channel log prefix. */
    readonly identity: string;
    /** Full pipe name (including the per-window endpoint suffix). */
    readonly pipeName: string;
    /** Exact stderr line the worker emits once its pipe server is ready. */
    readonly readyMarker: string;
    /** Executable to spawn. */
    readonly command: string;
    /** Arguments passed to {@link command}. */
    readonly args: readonly string[];
}

/**
 * Per-window lifecycle manager for the three `AutoContext.Worker.*`
 * processes (Workspace, DotNet, Web).
 *
 * Generates a single random 12-character endpoint suffix at
 * construction time and uses it to build per-window pipe names
 * (e.g. `autocontext.workspace-worker-abc123def456`). The same
 * suffix is later passed to `Mcp.Server` as `--endpoint-suffix` so
 * every process in one window talks on the same pipes while a second
 * window stays isolated.
 *
 * `Mcp.Server` itself is _not_ managed here — VS Code spawns it from
 * the {@link vscode.McpStdioServerDefinition} returned by
 * `McpServerProvider`.
 */
export class WorkerManager implements vscode.Disposable {
    private readonly endpointSuffix = randomUUID().replace(/-/g, '').slice(0, 12);
    private readonly children: ChildProcess[] = [];
    private readonly readyPromises = new Map<string, Promise<void>>();
    private readonly readyResolvers = new Map<string, () => void>();
    private started = false;

    constructor(
        private readonly extensionPath: string,
        private readonly outputChannel: vscode.OutputChannel,
        private readonly workspaceRoot: string | undefined,
        private readonly serversManifest: ServersManifest,
    ) {
        for (const entry of serversManifest.workers) {
            const identity = entry.name.replace(/^AutoContext\./, '');
            const promise = new Promise<void>(resolve => this.readyResolvers.set(identity, resolve));
            this.readyPromises.set(identity, promise);
        }
    }

    /** Per-window suffix appended to every pipe name. */
    getEndpointSuffix(): string {
        return this.endpointSuffix;
    }

    /**
     * Resolves when `Worker.Workspace` has emitted its ready marker.
     * Config reads and EditorConfig resolution depend on this worker,
     * so callers that need either should await this before proceeding.
     */
    whenWorkspaceReady(): Promise<void> {
        return this.readyPromises.get('Worker.Workspace')!;
    }

    /** Spawns all worker processes listed in the servers manifest. Idempotent. */
    start(): void {
        if (this.started) {
            return;
        }
        this.started = true;

        const exeSuffix = process.platform === 'win32' ? '.exe' : '';
        const serversPath = join(this.extensionPath, 'servers');

        const specs: WorkerSpec[] = this.serversManifest.workers.map(entry => {
            const identity = entry.name.replace(/^AutoContext\./, '');
            const pipe = formatEndpoint(entry.id, this.endpointSuffix);
            const serverDir = join(serversPath, entry.name);

            const command = entry.type === 'node'
                ? 'node'
                : join(serverDir, `${entry.name}${exeSuffix}`);

            const args: string[] = entry.type === 'node'
                ? [join(serverDir, 'index.js'), '--pipe', pipe]
                : ['--pipe', pipe];

            if (entry.id === 'workspace' && this.workspaceRoot) {
                args.push('--workspace-root', this.workspaceRoot);
            }

            return {
                identity,
                pipeName: pipe,
                readyMarker: `[${entry.name}] Ready.`,
                command,
                args,
            };
        });

        for (const spec of specs) {
            this.spawnWorker(spec);
        }
    }

    dispose(): void {
        for (const child of this.children) {
            child.kill();
        }
        this.children.length = 0;
    }

    private spawnWorker(spec: WorkerSpec): void {
        const child = spawn(spec.command, [...spec.args], {
            stdio: ['ignore', 'pipe', 'pipe'],
        });
        this.children.push(child);

        if (child.stderr) {
            const rl = createInterface({ input: child.stderr });
            rl.on('line', (line: string) => {
                this.outputChannel.appendLine(`[${spec.identity}] ${line}`);
                if (line === spec.readyMarker) {
                    const resolve = this.readyResolvers.get(spec.identity);
                    if (resolve) {
                        this.readyResolvers.delete(spec.identity);
                        resolve();
                    }
                }
            });
        }

        child.on('error', (err) => {
            this.outputChannel.appendLine(`[${spec.identity}] Failed to start: ${err.message}`);
        });

        child.on('exit', (code) => {
            this.outputChannel.appendLine(`[${spec.identity}] Exited with code ${code}`);
        });
    }
}
