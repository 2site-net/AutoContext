import { spawn } from 'node:child_process';

import type { LoggerFacade } from '#types/logger-facade.js';
import { EngineUnavailableError } from './engine-errors.js';

/** Launch specification for one engine process. */
export interface EngineSpawnRequest {
    /** Absolute workspace path forwarded on `--workspace`. */
    readonly workspacePath: string;

    /** Per-launch UUID forwarded on `--instance-id`. */
    readonly instanceId: string;

    /** Descriptor forwarded on `--instance-label`; omitted when empty. */
    readonly instanceLabel?: string;

    /** Whole seconds forwarded on `--idle-timeout`; omitted when unset. */
    readonly idleTimeoutSeconds?: number;

    /** Absolute path forwarded on `--cache-root`; omitted when unset. */
    readonly cacheRoot?: string;

    /** Absolute path of the engine binary to start. */
    readonly engineBinaryPath: string;
}

/**
 * Builds the engine argv in the order the daemon role's parser
 * expects. Mirrors the C# `EngineSpawner` argument list.
 */
export function buildEngineArgv(request: EngineSpawnRequest): string[] {
    const argv = [
        '--workspace',
        request.workspacePath,
        '--instance-id',
        request.instanceId,
    ];

    if (request.instanceLabel !== undefined && request.instanceLabel.length > 0) {
        argv.push('--instance-label', request.instanceLabel);
    }

    if (request.idleTimeoutSeconds !== undefined) {
        argv.push('--idle-timeout', Math.trunc(request.idleTimeoutSeconds).toString(10));
    }

    if (request.cacheRoot !== undefined && request.cacheRoot.length > 0) {
        argv.push('--cache-root', request.cacheRoot);
    }

    return argv;
}

/**
 * Starts engine processes. The child is unreferenced and its stdio is
 * discarded, so it outlives this process's event loop the way a daemon
 * should — the engine bounds its own lifetime through `--idle-timeout`
 * and its parent-pid watchdog, and logs to files rather than stdout.
 *
 * Counterpart of the C# `EngineSpawner` in `AutoContext.Client.Core`.
 */
export class EngineSpawner {
    private readonly logger: LoggerFacade;

    constructor(logger: LoggerFacade) {
        this.logger = logger;
    }

    /**
     * Starts one engine process and returns without waiting for it.
     *
     * @throws {EngineUnavailableError} When the binary cannot start.
     */
    spawn(request: EngineSpawnRequest): void {
        const argv = buildEngineArgv(request);

        try {
            const child = spawn(request.engineBinaryPath, argv, {
                stdio: 'ignore',
                windowsHide: true,
            });

            // A failed exec surfaces asynchronously; without a listener
            // it would reach the process-level error path.
            child.on('error', (err) => {
                this.logger.warn(
                    `Engine binary '${request.engineBinaryPath}' failed to start: ${err.message}`);
            });

            child.unref();
            this.logger.debug(`Spawned engine '${request.engineBinaryPath}' (pid ${child.pid}).`);
        }
        catch (err) {
            throw new EngineUnavailableError(
                `Failed to start the engine binary at '${request.engineBinaryPath}'.`,
                { cause: err });
        }
    }
}
