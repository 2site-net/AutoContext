import { describe, it, expect } from 'vitest';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { createFakeLogger } from '#support/logging/fake-logger.js';
import { EndpointKind, createInstanceId, formatEndpoint } from '#src/engine/endpoint.js';
import { EngineDaemonManager } from '#src/engine/engine-daemon-manager.js';
import {
    EngineRpcError,
    EngineSubscriptionDroppedError,
    EngineUnavailableError,
} from '#src/engine/engine-errors.js';
import { PROTOCOL_VERSION } from '#src/engine/json-rpc.js';
import { LengthPrefixedFrameCodec } from '#src/pipes/length-prefixed-frame-codec.js';
import { PipeListener } from '#src/pipes/pipe-listener.js';
import { computeWorkspaceHash } from '#src/engine/workspace-hash.js';

interface FakeRequest {
    readonly method: string;
    readonly params?: unknown;
    readonly id?: number;
}

type FakeReply =
    | { readonly result: unknown }
    | { readonly error: { readonly code: number; readonly message: string } }
    | { readonly stream: readonly unknown[] };

interface FakeEngine {
    readonly received: FakeRequest[];
    stop: () => Promise<void>;
}

/**
 * Binds an engine-shaped listener on the address the manager derives
 * for `workspacePath` + `instanceId`, answering Engine.Hello and each
 * supplied handler.
 */
async function startFakeEngine(
    workspacePath: string,
    instanceId: string,
    handlers: Readonly<Record<string, FakeReply>>,
): Promise<FakeEngine> {
    const address = formatEndpoint(
        EndpointKind.Rpc, computeWorkspaceHash(workspacePath), instanceId);
    const received: FakeRequest[] = [];
    const bound = await new PipeListener(address, createFakeLogger()).bind();
    const ac = new AbortController();

    const runTask = bound.run(async (socket, signal) => {
        const codec = new LengthPrefixedFrameCodec(socket);
        try {
            for (;;) {
                const raw = await codec.read(signal);
                if (raw === null) {
                    return;
                }

                const request = JSON.parse(raw.toString('utf8')) as FakeRequest;
                received.push(request);

                if (request.id === undefined) {
                    continue;
                }

                for (const frame of renderFrames(request, handlers)) {
                    await codec.write(Buffer.from(JSON.stringify(frame), 'utf8'), signal);
                }
            }
        }
        catch {
            // The connection tore down; the accept loop reclaims the socket.
        }
    }, ac.signal);

    return {
        received,
        stop: async () => {
            ac.abort();
            await runTask;
            await bound.dispose();
        },
    };
}

function renderFrames(
    request: FakeRequest,
    handlers: Readonly<Record<string, FakeReply>>,
): readonly unknown[] {
    const id = request.id;

    if (request.method === 'Engine.Hello') {
        return [{
            jsonrpc: '2.0',
            id,
            result: { protocolVersion: PROTOCOL_VERSION, engineVersion: '0.0.0-test' },
        }];
    }

    const reply = handlers[request.method];
    if (reply === undefined) {
        return [{
            jsonrpc: '2.0',
            id,
            error: { code: -32601, message: `Method not found: ${request.method}` },
        }];
    }

    if ('stream' in reply) {
        return [
            ...reply.stream.map((result) => ({ jsonrpc: '2.0', id, kind: 'next', result })),
            { jsonrpc: '2.0', id, kind: 'complete' },
        ];
    }

    return [{ jsonrpc: '2.0', id, ...reply }];
}

function createManager(workspacePath: string, instanceId: string): EngineDaemonManager {
    return new EngineDaemonManager({
        workspacePath,
        instanceId,
        engineBinaryPath: join(tmpdir(), 'autocontext-engine-absent'),
        spawnDisabled: true,
        logger: createFakeLogger(),
    });
}

function createWorkspacePath(): string {
    return join(tmpdir(), `ac-ws-${Math.random().toString(36).slice(2, 10)}`);
}

describe('EngineDaemonManager', () => {
    it('handshakes and returns a unary result', async () => {
        const workspacePath = createWorkspacePath();
        const instanceId = createInstanceId();
        const engine = await startFakeEngine(workspacePath, instanceId, {
            'Workspace.Info': {
                result: {
                    engineVersion: '1.2.3',
                    idleTimeout: '00:00:00',
                    instanceId,
                    instanceLabel: 'test',
                    revision: 7,
                },
            },
        });
        const manager = createManager(workspacePath, instanceId);
        try {
            const info = await manager.workspaceInfo();

            expect(info.engineVersion).toBe('1.2.3');
            expect(engine.received[0]?.method).toBe('Engine.Hello');
            expect(engine.received[1]?.method).toBe('Workspace.Info');
        }
        finally {
            await manager.dispose();
            await engine.stop();
        }
    });

    it('reuses one connection across calls', async () => {
        const workspacePath = createWorkspacePath();
        const instanceId = createInstanceId();
        const engine = await startFakeEngine(workspacePath, instanceId, {
            'Config.Get': { result: { instructions: [], mcpTools: [] } },
        });
        const manager = createManager(workspacePath, instanceId);
        try {
            await manager.configGet();
            await manager.configGet();

            const handshakes = engine.received.filter((r) => r.method === 'Engine.Hello');
            expect(handshakes).toHaveLength(1);
        }
        finally {
            await manager.dispose();
            await engine.stop();
        }
    });

    it('spawns at most one engine for a burst of concurrent first calls', async () => {
        const workspacePath = createWorkspacePath();
        const instanceId = createInstanceId();
        const engine = await startFakeEngine(workspacePath, instanceId, {
            'Config.Get': { result: { instructions: [], mcpTools: [] } },
        });
        const manager = createManager(workspacePath, instanceId);
        try {
            await Promise.all([manager.configGet(), manager.configGet(), manager.configGet()]);

            const handshakes = engine.received.filter((r) => r.method === 'Engine.Hello');
            expect(handshakes).toHaveLength(1);
        }
        finally {
            await manager.dispose();
            await engine.stop();
        }
    });

    it('sends the toggle parameters the engine expects', async () => {
        const workspacePath = createWorkspacePath();
        const instanceId = createInstanceId();
        const engine = await startFakeEngine(workspacePath, instanceId, {
            'Config.ToggleRule': { result: { instructions: [], mcpTools: [] } },
        });
        const manager = createManager(workspacePath, instanceId);
        try {
            await manager.configToggleRule('testing', 'INST0007');

            const toggle = engine.received.find((r) => r.method === 'Config.ToggleRule');
            expect(toggle?.params).toEqual({ name: 'testing', ruleId: 'INST0007' });
        }
        finally {
            await manager.dispose();
            await engine.stop();
        }
    });

    it('sends tool arguments under the arguments key', async () => {
        const workspacePath = createWorkspacePath();
        const instanceId = createInstanceId();
        const engine = await startFakeEngine(workspacePath, instanceId, {
            'McpTools.Invoke': { result: { kind: 'ok', name: 'read_editorconfig', content: [] } },
        });
        const manager = createManager(workspacePath, instanceId);
        try {
            const result = await manager.mcpToolsInvoke('read_editorconfig', { path: 'a.cs' });

            expect(result.kind).toBe('ok');
            const invoke = engine.received.find((r) => r.method === 'McpTools.Invoke');
            expect(invoke?.params).toEqual({
                name: 'read_editorconfig',
                arguments: { path: 'a.cs' },
            });
        }
        finally {
            await manager.dispose();
            await engine.stop();
        }
    });

    it('raises a typed error when the engine rejects a request', async () => {
        const workspacePath = createWorkspacePath();
        const instanceId = createInstanceId();
        const engine = await startFakeEngine(workspacePath, instanceId, {
            'Config.Get': { error: { code: -32602, message: 'Invalid params' } },
        });
        const manager = createManager(workspacePath, instanceId);
        try {
            await expect(manager.configGet()).rejects.toThrow(EngineRpcError);
        }
        finally {
            await manager.dispose();
            await engine.stop();
        }
    });

    it('fires an agent notification without awaiting a response', async () => {
        const workspacePath = createWorkspacePath();
        const instanceId = createInstanceId();
        const engine = await startFakeEngine(workspacePath, instanceId, {});
        const manager = createManager(workspacePath, instanceId);
        try {
            await manager.agentToolUsed('session-1', 'read_editorconfig', 'ok');

            const notification = engine.received.find((r) => r.method === 'Agent.ToolUsed');
            expect(notification?.id).toBeUndefined();
            expect(notification?.params).toEqual({
                sessionId: 'session-1',
                toolName: 'read_editorconfig',
                outcome: 'ok',
            });
        }
        finally {
            await manager.dispose();
            await engine.stop();
        }
    });

    it('yields each config snapshot frame of a subscription', async () => {
        const workspacePath = createWorkspacePath();
        const instanceId = createInstanceId();
        const engine = await startFakeEngine(workspacePath, instanceId, {
            'Config.Subscribe': {
                stream: [
                    { kind: 'snapshot', snapshot: { version: '1', instructions: [], mcpTools: [] } },
                    { kind: 'snapshot', snapshot: { version: '2', instructions: [], mcpTools: [] } },
                ],
            },
        });
        const manager = createManager(workspacePath, instanceId);
        try {
            const versions: (string | undefined)[] = [];
            for await (const snapshot of manager.subscribeConfig()) {
                versions.push(snapshot.version);
            }

            expect(versions).toEqual(['1', '2']);
        }
        finally {
            await manager.dispose();
            await engine.stop();
        }
    });

    it('raises a typed error when the engine drops a subscription', async () => {
        const workspacePath = createWorkspacePath();
        const instanceId = createInstanceId();
        const engine = await startFakeEngine(workspacePath, instanceId, {
            'Config.Subscribe': {
                stream: [{ kind: 'dropped', reason: 'slow-subscriber' }],
            },
        });
        const manager = createManager(workspacePath, instanceId);
        try {
            const drain = (async () => {
                for await (const _ of manager.subscribeConfig()) {
                    // Drained for the terminal frame.
                }
            })();

            await expect(drain).rejects.toThrow(EngineSubscriptionDroppedError);
        }
        finally {
            await manager.dispose();
            await engine.stop();
        }
    });

    it('reports the engine as unavailable when nothing is listening and spawning is off',
        async () => {
            const manager = createManager(createWorkspacePath(), createInstanceId());
            try {
                await expect(manager.workspaceInfo()).rejects.toThrow(EngineUnavailableError);
            }
            finally {
                await manager.dispose();
            }
        });

    it('rejects calls issued after dispose', async () => {
        const manager = createManager(createWorkspacePath(), createInstanceId());
        await manager.dispose();

        await expect(manager.workspaceInfo()).rejects.toThrow(/disposed/);
    });

    it('yields worker log records and passes the worker id', async () => {
        const workspacePath = createWorkspacePath();
        const instanceId = createInstanceId();
        const engine = await startFakeEngine(workspacePath, instanceId, {
            'Logs.TailWorker': {
                stream: [
                    { kind: 'record', record: { message: 'first' } },
                    { kind: 'record', record: { message: 'second' } },
                ],
            },
        });
        const manager = createManager(workspacePath, instanceId);
        try {
            const messages: string[] = [];
            for await (const record of manager.tailWorkerLogs('workspace')) {
                messages.push(record.message);
            }

            expect(messages).toEqual(['first', 'second']);
            const tail = engine.received.find((r) => r.method === 'Logs.TailWorker');
            expect(tail?.params).toEqual({ workerId: 'workspace' });
        }
        finally {
            await manager.dispose();
            await engine.stop();
        }
    });

    it('ends a worker log tail when the engine reports an unknown worker', async () => {
        const workspacePath = createWorkspacePath();
        const instanceId = createInstanceId();
        const engine = await startFakeEngine(workspacePath, instanceId, {
            'Logs.TailWorker': { stream: [{ kind: 'not-found', workerId: 'ghost' }] },
        });
        const manager = createManager(workspacePath, instanceId);
        try {
            const records = [];
            for await (const record of manager.tailWorkerLogs('ghost')) {
                records.push(record);
            }

            expect(records).toHaveLength(0);
        }
        finally {
            await manager.dispose();
            await engine.stop();
        }
    });

    it('rejects a subscription issued after dispose', async () => {
        const manager = createManager(createWorkspacePath(), createInstanceId());
        await manager.dispose();

        const stream = manager.subscribeConfig();
        await expect(stream.next()).rejects.toThrow(/disposed/);
    });

    it('exposes the endpoint address it dials', () => {
        const workspacePath = createWorkspacePath();
        const instanceId = createInstanceId();
        const manager = createManager(workspacePath, instanceId);

        expect(manager.rpcAddress).toBe(formatEndpoint(
            EndpointKind.Rpc, computeWorkspaceHash(workspacePath), instanceId));
        expect(manager.instanceId).toBe(instanceId);
    });
});
