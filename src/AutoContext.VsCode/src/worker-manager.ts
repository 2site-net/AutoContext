import * as vscode from 'vscode';
import { join } from 'node:path';
import { spawn, type ChildProcess } from 'node:child_process';
import { createInterface } from 'node:readline';
import { formatEndpoint } from './endpoint-formatter.js';
import { IdentifierFactory } from './identifier-factory.js';
import type { ServerEntry } from './server-entry.js';
import type { Logger } from '#types/logger.js';

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
    private readonly endpointSuffix = IdentifierFactory.createRandomId();
    private readonly children: ChildProcess[] = [];
    private readonly readyPromises = new Map<string, Promise<void>>();
    private readonly readyResolvers = new Map<string, { resolve: () => void; reject: (err: Error) => void }>();
    private started = false;
    private disposed = false;

    constructor(
        private readonly extensionPath: string,
        private readonly logger: Logger,
        private readonly workspaceRoot: string | undefined,
        private readonly workers: readonly ServerEntry[],
        private readonly logPipeName?: string,
    ) {
        for (const entry of workers) {
            const identity = entry.getShortName();
            const promise = new Promise<void>((resolve, reject) =>
                this.readyResolvers.set(identity, { resolve, reject }));
            // Swallow rejection on the stored promise so callers that never
            // attach a handler (or attach only via Promise.race) don't surface
            // an unhandled rejection when dispose() rejects pending waiters.
            promise.catch(() => { /* see whenWorkspaceReady() */ });
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
            this.logger.debug('start() called again; ignoring (already started)');
            return;
        }
        this.started = true;
        this.logger.info(`Starting ${this.workers.length} worker(s) with endpoint suffix '${this.endpointSuffix}'`);

        const exeSuffix = process.platform === 'win32' ? '.exe' : '';
        const serversPath = join(this.extensionPath, 'servers');

        const specs: WorkerSpec[] = this.workers.map(entry => {
            const identity = entry.getShortName();
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

            // Workers that understand --log-pipe stream structured
            // logger output back over the LogServer's named pipe.
            // Workers that don't simply ignore the flag.
            if (this.logPipeName) {
                args.push('--log-pipe', this.logPipeName);
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
        if (this.disposed) {
            return;
        }
        this.disposed = true;

        // Release any callers awaiting a worker's ready marker — without
        // this, a caller that hits dispose() before the worker emitted
        // its ready line would hang forever.
        for (const [, { reject }] of this.readyResolvers) {
            reject(new Error('WorkerManager disposed before worker became ready.'));
        }
        this.readyResolvers.clear();

        for (const child of this.children) {
            child.kill();
        }
        this.children.length = 0;
    }

    private spawnWorker(spec: WorkerSpec): void {
        const workerLogger = this.logger.forCategory(spec.identity);
        workerLogger.debug(`Spawning: ${spec.command} ${spec.args.join(' ')}`);
        const child = spawn(spec.command, [...spec.args], {
            stdio: ['ignore', 'pipe', 'pipe'],
        });
        workerLogger.debug(`Spawned (pid=${child.pid ?? 'unknown'}); waiting for ready marker on '${spec.pipeName}'`);
        this.children.push(child);

        if (child.stderr) {
            const rl = createInterface({ input: child.stderr });
            rl.on('line', (line: string) => {
                workerLogger.info(line);
                if (line === spec.readyMarker) {
                    workerLogger.debug('Ready marker received');
                    const entry = this.readyResolvers.get(spec.identity);
                    if (entry) {
                        this.readyResolvers.delete(spec.identity);
                        entry.resolve();
                    }
                }
            });
        }

        child.on('error', (err) => {
            workerLogger.error('Failed to start', err);
            const entry = this.readyResolvers.get(spec.identity);
            if (entry) {
                this.readyResolvers.delete(spec.identity);
                entry.reject(err);
            }
        });

        child.on('exit', (code) => {
            workerLogger.info(`Exited with code ${code}`);
            const entry = this.readyResolvers.get(spec.identity);
            if (entry) {
                this.readyResolvers.delete(spec.identity);
                entry.reject(new Error(`Worker '${spec.identity}' exited with code ${code} before becoming ready.`));
            }
        });
    }
}
