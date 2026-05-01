import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { connect, type Socket } from 'node:net';
import { AutoContextConfigServer } from '#src/autocontext-config-server';
import { AutoContextConfig } from '#src/autocontext-config';
import type { AutoContextConfigManager } from '#src/autocontext-config-manager';
import { createFakeLogger } from '#testing/fakes';
import { pipePath } from '#testing/utils/pipe-helpers';
import { waitFor } from '#testing/utils/wait-for';

const HEADER_BYTES = 4;
const SUFFIX = 'cfgsrv' + Math.random().toString(16).slice(2, 10);

interface ListenerSlot {
    listener?: () => void;
}

function makeConfigManager(initial: AutoContextConfig, slot: ListenerSlot): AutoContextConfigManager {
    let current = initial;
    return {
        read: vi.fn(async () => current),
        onDidChange: vi.fn((cb: () => void) => {
            slot.listener = cb;
            return { dispose: vi.fn() };
        }),
        // Test-only mutator (not part of the public type).
        _set(next: AutoContextConfig) { current = next; },
    } as unknown as AutoContextConfigManager;
}

async function connectClient(pipeName: string): Promise<Socket> {
    return new Promise((resolve, reject) => {
        const socket = connect(pipePath(pipeName), () => resolve(socket));
        socket.once('error', reject);
    });
}

async function readOneFrame(socket: Socket): Promise<unknown> {
    return new Promise((resolve, reject) => {
        let buffer = Buffer.alloc(0);
        const onData = (chunk: Buffer): void => {
            buffer = Buffer.concat([buffer, chunk]);
            if (buffer.length < HEADER_BYTES) { return; }
            const length = buffer.readInt32LE(0);
            if (buffer.length < HEADER_BYTES + length) { return; }
            const payload = buffer.subarray(HEADER_BYTES, HEADER_BYTES + length);
            socket.off('data', onData);
            try {
                resolve(JSON.parse(payload.toString('utf8')));
            }
            catch (err) {
                reject(err);
            }
        };
        socket.on('data', onData);
        socket.once('error', reject);
    });
}

describe('AutoContextConfigServer', () => {
    let server: AutoContextConfigServer;
    let manager: AutoContextConfigManager;
    let slot: ListenerSlot;
    const logger = createFakeLogger();

    beforeEach(() => {
        vi.clearAllMocks();
        slot = {};
        manager = makeConfigManager(
            new AutoContextConfig({
                mcpTools: {
                    alpha: false,
                    beta: { disabledTasks: ['scan'] },
                },
            }),
            slot,
        );
        server = new AutoContextConfigServer(manager, SUFFIX, logger);
        server.start();
    });

    afterEach(() => {
        server.dispose();
    });

    it('exposes a deterministic pipe name keyed off the instance id', () => {
        expect(server.getPipeName()).toBe(`autocontext.extension-config#${SUFFIX}`);
    });

    it('subscribes to config-manager change events on construction', () => {
        expect(slot.listener).toBeTypeOf('function');
    });

    it('pushes the current snapshot as the handshake frame on connect', async () => {
        const socket = await connectClient(server.getPipeName());

        const frame = await readOneFrame(socket);

        expect(frame).toEqual({
            disabledTools: ['alpha'],
            disabledTasks: { beta: ['scan'] },
        });

        socket.destroy();
    });

    it('rebroadcasts a fresh snapshot to live subscribers when the config changes', async () => {
        const socket = await connectClient(server.getPipeName());

        // Drain the initial handshake frame.
        await readOneFrame(socket);

        // Rotate the config and fire onDidChange.
        (manager as unknown as { _set(next: AutoContextConfig): void })._set(
            new AutoContextConfig({ mcpTools: { gamma: false } }),
        );
        slot.listener!();

        const frame = await readOneFrame(socket);

        expect(frame).toEqual({
            disabledTools: ['gamma'],
            disabledTasks: {},
        });

        socket.destroy();
    });

    it('skips broadcasting when no clients are connected', async () => {
        // No connected sockets at all; firing the listener should be a silent no-op.
        slot.listener!();

        // Wait one event-loop tick so any unintended async work would surface.
        await new Promise(resolve => setImmediate(resolve));

        // read() is called only on connect — never via the empty-broadcast path.
        expect(manager.read).not.toHaveBeenCalled();
    });

    it('closes connected sockets and the server on dispose', async () => {
        const socket = await connectClient(server.getPipeName());
        await readOneFrame(socket);

        const closed = new Promise<void>(resolve => socket.once('close', () => resolve()));
        server.dispose();

        await waitFor(() => socket.destroyed, 2000);
        await closed;
    });
});
