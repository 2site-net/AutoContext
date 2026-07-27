import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { platform, tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createFakeLogger } from '../logging/fake-logger.js';
import { EngineDaemonManager } from '#src/engine/engine-daemon-manager.js';
import { createInstanceId } from '#src/engine/endpoint.js';

const BINARY_NAME = platform() === 'win32' ? 'autocontext-engine.exe' : 'autocontext-engine';

/**
 * Absolute path of the engine binary produced by the .NET build, or
 * `undefined` when this stack has not been built yet.
 *
 * The round-trip suite is the one place the TypeScript tests depend on
 * a .NET artefact, so it reports the absence rather than failing a
 * TypeScript-only run.
 */
export function findEngineBinary(): string | undefined {
    const packageRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
    const engineRoot = resolve(packageRoot, '../AutoContext.Engine/bin');

    for (const configuration of ['Release', 'Debug']) {
        const candidate = join(engineRoot, configuration, 'net10.0', BINARY_NAME);
        if (existsSync(candidate)) {
            return candidate;
        }
    }

    return undefined;
}

/** A spawned engine plus the temporary tree it was pointed at. */
export interface SpawnedEngine {
    readonly manager: EngineDaemonManager;
    readonly workspacePath: string;
    readonly instanceId: string;
    readonly cacheRoot: string;

    /** Opens a second manager against the same engine instance. */
    attach: () => EngineDaemonManager;

    /** Shuts the engine down and removes the temporary tree. */
    stop: () => Promise<void>;
}

/**
 * Creates a throwaway workspace, then resolves a manager for it —
 * spawning a real engine because nothing is listening yet.
 *
 * The engine is given its own cache root so it never touches the
 * developer's real one, and a non-zero idle timeout so a leaked engine
 * eventually exits on its own.
 */
export async function spawnEngine(engineBinaryPath: string): Promise<SpawnedEngine> {
    const root = await mkdtemp(join(tmpdir(), 'autocontext-rt-'));
    const workspacePath = join(root, 'workspace');
    const cacheRoot = join(root, 'cache');

    await mkdir(workspacePath, { recursive: true });
    await writeFile(join(workspacePath, 'sample.cs'), 'class Sample { }\n', 'utf8');
    await writeFile(join(workspacePath, 'sample.ts'), 'export const sample = 1;\n', 'utf8');

    const instanceId = createInstanceId();
    const attached: EngineDaemonManager[] = [];

    const create = (): EngineDaemonManager => {
        const manager = new EngineDaemonManager({
            workspacePath,
            instanceId,
            cacheRoot,
            engineBinaryPath,
            instanceLabel: 'nodejs-round-trip',
            idleTimeoutSeconds: 120,
            logger: createFakeLogger(),
        });
        attached.push(manager);
        return manager;
    };

    const manager = create();
    await manager.start();

    return {
        manager,
        workspacePath,
        instanceId,
        cacheRoot,
        attach: create,
        stop: async () => {
            try {
                await manager.shutdown();
            }
            catch {
                // The engine may already be gone; teardown continues.
            }

            for (const open of attached) {
                await open.dispose();
            }

            await rm(root, { recursive: true, force: true, maxRetries: 5 });
        },
    };
}
