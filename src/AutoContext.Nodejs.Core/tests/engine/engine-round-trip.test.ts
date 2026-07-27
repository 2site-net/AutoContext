import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { readFile, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import {
    findEngineBinary,
    spawnEngine,
    type SpawnedEngine,
} from '#support/engine/spawned-engine.js';
import { EngineDaemonManager } from '#src/engine/engine-daemon-manager.js';
import { createInstanceId } from '#src/engine/endpoint.js';
import { EngineUnavailableError } from '#src/engine/engine-errors.js';

const engineBinaryPath = findEngineBinary();

// The engine binary comes from the .NET half of the build. A
// TypeScript-only run has nothing to dial, so the suite reports the
// gap instead of failing.
const suite = engineBinaryPath === undefined ? describe.skip : describe;

const SPAWN_TIMEOUT = 60_000;

/** Drains at most `count` items so a live stream cannot hang a test. */
async function take<T>(stream: AsyncGenerator<T>, count: number): Promise<T[]> {
    const items: T[] = [];
    for await (const item of stream) {
        items.push(item);
        if (items.length >= count) {
            break;
        }
    }
    return items;
}

suite('EngineDaemonManager against a spawned engine', () => {
    let engine: SpawnedEngine;
    let manager: EngineDaemonManager;

    beforeAll(async () => {
        engine = await spawnEngine(engineBinaryPath!);
        manager = engine.manager;
    }, SPAWN_TIMEOUT);

    afterAll(async () => {
        await engine.stop();
    }, SPAWN_TIMEOUT);

    describe('lifecycle', () => {
        it('spawns an engine when none is listening and completes the handshake', async () => {
            const info = await manager.workspaceInfo();

            expect(info.instanceId).toBe(engine.instanceId);
            expect(info.engineVersion).not.toBe('');
            expect(info.instanceLabel).toBe('nodejs-round-trip');
        });

        it('reuses the running engine for a second manager on the same instance', async () => {
            const second = engine.attach();

            const first = await manager.workspaceInfo();
            const other = await second.workspaceInfo();

            expect(other.instanceId).toBe(first.instanceId);
            expect(other.engineVersion).toBe(first.engineVersion);
        }, SPAWN_TIMEOUT);

        it('reports this engine in the shared registry', async () => {
            const result = await manager.registryEntries();
            const mine = result.entries.find((e) => e.instanceId === engine.instanceId);

            expect(mine).toBeDefined();
            expect(mine?.workspacePath.toLowerCase())
                .toBe(engine.workspacePath.toLowerCase());
        });

        it('fails fast when spawning is disabled and nothing is listening', async () => {
            const orphan = new EngineDaemonManager({
                workspacePath: engine.workspacePath,
                instanceId: createInstanceId(),
                engineBinaryPath: engineBinaryPath!,
                spawnDisabled: true,
            });
            try {
                await expect(orphan.workspaceInfo()).rejects.toThrow(EngineUnavailableError);
            }
            finally {
                await orphan.dispose();
            }
        });
    });

    describe('workspace', () => {
        it('detects the workspace contents', async () => {
            const detected = await manager.workspaceDetect();

            expect(detected.flags['hasCSharp']).toBe(true);
            expect(detected.flags['hasTypeScript']).toBe(true);
            // The engine reports bare extensions, without a leading dot.
            expect(detected.extensions).toContain('cs');
        });
    });

    describe('instructions', () => {
        it('lists the bundled corpus', async () => {
            const listed = await manager.instructionsList();

            expect(listed.files.length).toBeGreaterThan(0);
            expect(listed.files.every((f) => f.source === 'bundled')).toBe(true);
        });

        it('returns the curatorial categories', async () => {
            const categories = await manager.instructionsCategories();

            expect(categories.categories.length).toBeGreaterThan(0);
        });

        it('projects a file body and reports an unknown one as not-found', async () => {
            // Instructions are addressed by their stable key; `name`
            // carries the display form, which the engine does not index.
            const listed = await manager.instructionsList();
            const key = listed.files[0]?.key ?? '';

            const found = await manager.instructionsGet(key);
            const missing = await manager.instructionsGet('no-such-instructions-file');

            expect(found.kind).toBe('ok');
            expect(missing.kind).toBe('not-found');
        });

        it('returns the raw bundled body', async () => {
            const listed = await manager.instructionsList();
            const key = listed.files[0]?.key ?? '';

            const raw = await manager.instructionsGetRaw(key, 'bundled');

            expect(raw.kind).toBe('ok');
            if (raw.kind === 'ok') {
                expect(raw.source).toBe('bundled');
                expect(raw.content?.length ?? 0).toBeGreaterThan(0);
            }
        });

        it('returns every enabled file and the always-attached subset', async () => {
            const all = await manager.instructionsGetAll();
            const attached = await manager.instructionsGetAlwaysAttached();

            expect(all.files.length).toBeGreaterThan(0);
            expect(attached.files.length).toBeGreaterThan(0);
            expect(attached.files.length).toBeLessThanOrEqual(all.files.length);
        });

        it('searches bodies by content', async () => {
            const hits = await manager.instructionsSearchContent('commit', { limit: 5 });

            expect(hits.hits.length).toBeGreaterThan(0);
        });

        it('searches by metadata and reports an unknown field', async () => {
            const matched = await manager.instructionsSearchByMetadata({ key: '.*' });
            const rejected = await manager.instructionsSearchByMetadata({ nope: true });

            expect(matched.kind).toBe('ok');
            if (matched.kind === 'ok') {
                expect(matched.results.length).toBeGreaterThan(0);
            }
            expect(rejected.kind).toBe('error');
            if (rejected.kind === 'error') {
                expect(rejected.error).toBe('unknown-field');
            }
        });
    });

    describe('config', () => {
        it('round-trips a file toggle through disk', async () => {
            const listed = await manager.instructionsList();
            const name = listed.files[0]?.name ?? '';

            const toggled = await manager.configToggleFile(name);
            try {
                const entry = toggled.instructions.find((f) => f.name === name);
                expect(entry?.disabled).toBe(true);

                const onDisk = await readFile(
                    join(engine.workspacePath, '.autocontext.json'), 'utf8');
                expect(onDisk).toContain(name);
            }
            finally {
                await manager.configToggleFile(name);
            }
        });

        it('round-trips a rule toggle', async () => {
            const listed = await manager.instructionsList();
            const name = listed.files[0]?.name ?? '';

            const toggled = await manager.configToggleRule(name, 'INST0001');
            try {
                const entry = toggled.instructions.find((f) => f.name === name);
                expect(entry?.rules.some((r) => r.id === 'INST0001' && r.disabled === true))
                    .toBe(true);
            }
            finally {
                await manager.configToggleRule(name, 'INST0001');
            }
        });
    });

    describe('mcp tools', () => {
        it('lists the registry', async () => {
            const listed = await manager.mcpToolsList();

            expect(listed.tools.length).toBeGreaterThan(0);
            expect(listed.tools.every((t) => (t.name ?? '') !== '')).toBe(true);
        });

        it('reports an unknown tool as not-found', async () => {
            const invoked = await manager.mcpToolsInvoke('no_such_tool', {});

            expect(invoked.kind).toBe('not-found');
        });
    });

    describe('discovery', () => {
        it('routes a prompt to tools and instructions', async () => {
            const routed = await manager.discoveryRouteForPrompt('review this c# file');

            expect(routed.matchedCategories.length + routed.matchedExtensions.length)
                .toBeGreaterThan(0);
        });

        it('routes a tool to instructions', async () => {
            const listed = await manager.mcpToolsList();
            const name = listed.tools[0]?.name ?? '';

            const routed = await manager.discoveryRouteForTool(name);

            expect(Array.isArray(routed.instructions)).toBe(true);
        });
    });

    describe('logs', () => {
        it('reads engine records', async () => {
            const read = await manager.logsGetEngine({ lastN: 5 });

            expect(read.records.length).toBeGreaterThan(0);
        });

        it('reports an unknown worker as not-found', async () => {
            const read = await manager.logsGetWorker('never-spawned-worker');

            expect(read.kind).toBe('not-found');
        });
    });

    describe('subscriptions', () => {
        it('replays the current config to a late subscriber', async () => {
            const snapshots = await take(manager.subscribeConfig(), 1);

            expect(snapshots).toHaveLength(1);
        }, SPAWN_TIMEOUT);

        it('replays the current instructions roster to a late subscriber', async () => {
            const rosters = await take(manager.subscribeInstructions(), 1);

            expect(rosters[0]?.length ?? 0).toBeGreaterThan(0);
        }, SPAWN_TIMEOUT);

        it('reports the engine as started on the lifecycle stream', async () => {
            const events = await take(manager.subscribeLifecycle(), 1);

            expect(events[0]?.kind).toBe('started');
            expect(events[0]?.instanceId).toBe(engine.instanceId);
        }, SPAWN_TIMEOUT);

        it('carries an agent notification through to a subscriber', async () => {
            const events = manager.subscribeAgentEvents();
            const first = take(events, 1);

            // The stream must be established before the notification is
            // fired; the engine fans out live events, not history.
            await new Promise((resolve) => setTimeout(resolve, 500));
            await manager.agentToolUsed('session-round-trip', 'read_editorconfig', 'ok');

            const received = await first;

            expect(received[0]?.kind).toBe('tool-used');
            expect(received[0]?.sessionId).toBe('session-round-trip');
        }, SPAWN_TIMEOUT);

        it('tails engine log records as they are written', async () => {
            const records = manager.tailEngineLogs();
            const first = take(records, 1);

            await new Promise((resolve) => setTimeout(resolve, 500));

            // A fresh connection makes the engine log its accepted
            // handshake. Calls on the held connection do not, so they
            // would leave the tail waiting.
            const pump = engine.attach();
            await pump.workspaceInfo();
            await pump.dispose();

            const received = await first;

            expect(received[0]?.category ?? '').not.toBe('');
        }, SPAWN_TIMEOUT);

        it('observes a config change written by another process', async () => {
            const snapshots = manager.subscribeConfig();
            const collected = take(snapshots, 2);

            await new Promise((resolve) => setTimeout(resolve, 500));
            await writeFile(
                join(engine.workspacePath, '.autocontext.json'),
                JSON.stringify({ version: '9.9.9', instructions: {}, mcpTools: {} }, null, 2),
                'utf8');

            const received = await collected;

            expect(received).toHaveLength(2);
            expect(received[1]?.version).toBe('9.9.9');
        }, SPAWN_TIMEOUT);
    });
});
