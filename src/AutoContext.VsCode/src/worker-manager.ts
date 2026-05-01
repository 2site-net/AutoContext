import * as vscode from 'vscode';
import { join } from 'node:path';
import { spawn, type ChildProcess } from 'node:child_process';
import { createInterface } from 'node:readline';
import { IdentifierFactory } from './identifier-factory.js';
import type { ServerEntry } from './server-entry.js';
import type { Logger } from '#types/logger.js';

/**
 * Static spawn parameters for one worker. Built once at
 * construction time and reused on every (re)spawn for that worker.
 */
interface WorkerSpec {
    /** Short identity used as the output-channel log prefix. */
    readonly identity: string;
    /** Full pipe name (including the per-window instance id). */
    readonly pipeName: string;
    /** Exact stderr line the worker emits once its pipe server is ready. */
    readonly readyMarker: string;
    /** Executable to spawn. */
    readonly command: string;
    /** Arguments passed to {@link command}. */
    readonly args: readonly string[];
}

/**
 * Per-worker lifecycle slot. Holds the static {@link WorkerSpec}
 * plus the live state for the current spawn (if any). When the
 * child exits, {@link readyPromise}, {@link resolver}, and
 * {@link child} are all cleared so the next {@link WorkerManager.ensureRunning}
 * call respawns cleanly.
 */
interface WorkerSlot {
    readonly spec: WorkerSpec;
    /** The currently spawned child, or undefined if no worker is alive. */
    child?: ChildProcess;
    /**
     * Resolves when the live child emits its ready marker; rejects
     * when it fails to start or exits before becoming ready.
     * Undefined when no spawn is in flight (idle or after exit).
     */
    readyPromise?: Promise<void>;
    /** Bound to {@link readyPromise}; cleared once it settles. */
    resolver?: { resolve: () => void; reject: (err: Error) => void };
}

/**
 * Per-window lifecycle manager for the three `AutoContext.Worker.*`
 * processes (Workspace, DotNet, Web).
 *
 * Workers self-format their own listen address from
 * `--instance-id <id>` and their compile-time {@link ServerEntry.id}
 * (e.g. `autocontext.worker-dotnet#abc123def456`). The same
 * `instance-id` is also passed to `Mcp.Server` so every process in
 * one window agrees on every pipe address while a second window
 * stays isolated.
 *
 * `Mcp.Server` itself is _not_ managed here — VS Code spawns it from
 * the {@link vscode.McpStdioServerDefinition} returned by
 * `McpServerProvider`.
 *
 * Spawn happens through {@link ensureRunning}, which coalesces
 * concurrent requests for the same worker and respawns automatically
 * after a previous child has exited. There is no eager-start step:
 * workers are spawned on demand by the orchestrator's worker-control
 * channel (when a tool call needs them) or by {@link whenWorkspaceReady}
 * during activation.
 */
export class WorkerManager implements vscode.Disposable {
    private readonly slots = new Map<string, WorkerSlot>();
    private disposed = false;

    constructor(
        private readonly extensionPath: string,
        private readonly logger: Logger,
        private readonly workspaceRoot: string | undefined,
        private readonly workers: readonly ServerEntry[],
        private readonly instanceId: string,
        private readonly logServiceAddress?: string,
        private readonly healthMonitorServiceAddress?: string,
    ) {
        for (const spec of this.buildSpecs()) {
            this.slots.set(spec.identity, { spec });
        }
    }

    /** Per-window instance id propagated to every spawned worker. */
    getInstanceId(): string {
        return this.instanceId;
    }

    /**
     * Convenience wrapper around `ensureRunning('Worker.Workspace')`.
     *
     * `Worker.Workspace` is the only worker the extension itself
     * depends on at runtime: it owns config reads (`.autocontext.json`,
     * `.editorconfig` resolution) that the activation flow, the
     * instructions pipeline, and several UI surfaces consume directly
     * — none of those go through `Mcp.Server`. The other workers
     * (DotNet, Web) only serve MCP tool calls and are awaited via the
     * orchestrator's control channel, never from extension code.
     *
     * Keeping this method:
     * 1. Pins the activation barrier (see `extension.ts`) to a named
     *    API rather than a stringly-typed `ensureRunning(...)` call,
     *    so removing the barrier is a discoverable refactor.
     * 2. Keeps the magic identity `'Worker.Workspace'` confined to
     *    this file.
     */
    whenWorkspaceReady(): Promise<void> {
        return this.ensureRunning('Worker.Workspace');
    }

    /**
     * Ensures the worker identified by {@link identity} is running and
     * has emitted its ready marker. Returns the in-flight ready
     * promise when a spawn is already underway (so concurrent callers
     * coalesce onto the same process). After a worker has exited the
     * next call respawns it.
     *
     * Rejects when the worker fails to start, exits before becoming
     * ready, or the manager has been disposed.
     */
    ensureRunning(identity: string): Promise<void> {
        if (this.disposed) {
            return Promise.reject(new Error('WorkerManager is disposed.'));
        }

        const slot = this.slots.get(identity);
        if (!slot) {
            return Promise.reject(new Error(`No worker registered with identity '${identity}'.`));
        }

        if (slot.readyPromise) {
            return slot.readyPromise;
        }

        slot.readyPromise = new Promise<void>((resolve, reject) => {
            slot.resolver = { resolve, reject };
        });
        // Swallow rejection on the stored promise so callers that never
        // attach a handler (or attach only via Promise.race) don't surface
        // an unhandled rejection when dispose() rejects pending waiters.
        slot.readyPromise.catch(() => { /* see whenWorkspaceReady() */ });

        this.spawnWorker(slot);

        return slot.readyPromise;
    }

    dispose(): void {
        if (this.disposed) {
            return;
        }
        this.disposed = true;

        for (const slot of this.slots.values()) {
            // Release any callers awaiting this worker's ready marker —
            // without this, a caller that hits dispose() before the
            // worker emitted its ready line would hang forever.
            if (slot.resolver) {
                slot.resolver.reject(new Error('WorkerManager disposed before worker became ready.'));
                slot.resolver = undefined;
            }
            slot.readyPromise = undefined;

            if (slot.child) {
                slot.child.kill();
                slot.child = undefined;
            }
        }
    }

    private buildSpecs(): WorkerSpec[] {
        const exeSuffix = process.platform === 'win32' ? '.exe' : '';
        const serversPath = join(this.extensionPath, 'servers');

        return this.workers.map(entry => {
            const identity = entry.getShortName();
            const pipe = IdentifierFactory.createServiceAddress(`worker-${entry.id}`, this.instanceId);
            const serverDir = join(serversPath, entry.name);

            const command = entry.type === 'node'
                ? 'node'
                : join(serverDir, `${entry.name}${exeSuffix}`);

            // Worker self-formats its listen address from --instance-id
            // plus its compile-time worker id; no --pipe needed.
            const args: string[] = entry.type === 'node'
                ? [join(serverDir, 'index.js'), '--instance-id', this.instanceId]
                : ['--instance-id', this.instanceId];

            if (entry.id === 'workspace' && this.workspaceRoot) {
                args.push('--workspace-root', this.workspaceRoot);
            }

            // Workers that understand --service log=<address> stream
            // structured logger output back over the LogServer's named
            // pipe. Workers that don't simply ignore the flag.
            if (this.logServiceAddress) {
                args.push('--service', `log=${this.logServiceAddress}`);
            }

            // Workers that understand --service health-monitor=<address>
            // connect to the extension's HealthMonitorServer named pipe,
            // write their worker id, and keep the socket open for the
            // lifetime of the process. The extension uses the socket
            // close to know the worker exited. Workers that don't
            // understand the switch simply ignore it.
            if (this.healthMonitorServiceAddress) {
                args.push('--service', `health-monitor=${this.healthMonitorServiceAddress}`);
            }

            return {
                identity,
                pipeName: pipe,
                readyMarker: `[${entry.name}] Ready.`,
                command,
                args,
            };
        });
    }

    private spawnWorker(slot: WorkerSlot): void {
        const spec = slot.spec;
        const workerLogger = this.logger.forCategory(spec.identity);
        workerLogger.debug(`Spawning: ${spec.command} ${spec.args.join(' ')}`);
        const child = spawn(spec.command, [...spec.args], {
            stdio: ['ignore', 'pipe', 'pipe'],
        });
        workerLogger.debug(`Spawned (pid=${child.pid ?? 'unknown'}); waiting for ready marker on '${spec.pipeName}'`);
        slot.child = child;

        if (child.stderr) {
            const rl = createInterface({ input: child.stderr });
            rl.on('line', (line: string) => {
                workerLogger.info(line);
                if (line === spec.readyMarker) {
                    workerLogger.debug('Ready marker received');
                    if (slot.resolver) {
                        const { resolve } = slot.resolver;
                        slot.resolver = undefined;
                        resolve();
                    }
                }
            });
        }

        child.on('error', (err) => {
            workerLogger.error('Failed to start', err);
            if (slot.resolver) {
                const { reject } = slot.resolver;
                slot.resolver = undefined;
                reject(err);
            }
            // Clear readyPromise so the next ensureRunning() respawns.
            slot.readyPromise = undefined;
            slot.child = undefined;
        });

        child.on('exit', (code) => {
            workerLogger.info(`Exited with code ${code}`);
            if (slot.resolver) {
                const { reject } = slot.resolver;
                slot.resolver = undefined;
                reject(new Error(`Worker '${spec.identity}' exited with code ${code} before becoming ready.`));
            }
            // Clear readyPromise so the next ensureRunning() respawns.
            slot.readyPromise = undefined;
            slot.child = undefined;
        });
    }
}

