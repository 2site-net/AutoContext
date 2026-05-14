import { describe, it, expect } from 'vitest';
import { PipeTransport } from '#src/pipes/pipe-transport.js';
import { PipeListener } from '#src/pipes/pipe-listener.js';
import { createFakeLogger, uniquePipeName } from './test-helpers.js';

describe('PipeTransport', () => {
    it('rejects an empty pipe name with TypeError', async () => {
        const transport = new PipeTransport(createFakeLogger());
        await expect(transport.connect('')).rejects.toBeInstanceOf(TypeError);
    });

    it('throws when the signal is already aborted', async () => {
        const transport = new PipeTransport(createFakeLogger());
        const ac = new AbortController();
        ac.abort();
        await expect(transport.connect(uniquePipeName(), ac.signal)).rejects.toThrow();
    });

    it('resolves to a writable socket when a server is listening', async () => {
        const name = uniquePipeName();
        const bound = await new PipeListener(name, createFakeLogger()).bind();
        const ac = new AbortController();
        const runTask = bound.run(async (socket) => {
            await new Promise<void>((resolve) => socket.once('close', () => resolve()));
        }, ac.signal);
        try {
            const transport = new PipeTransport(createFakeLogger());
            const socket = await transport.connect(name);
            try {
                expect(socket.writable).toBe(true);
            }
            finally {
                socket.destroy();
            }
        }
        finally {
            ac.abort();
            await runTask;
            await bound.dispose();
        }
    });

    it('rejects when no server is listening on the pipe', async () => {
        const transport = new PipeTransport(createFakeLogger());
        await expect(transport.connect(uniquePipeName())).rejects.toThrow();
    });
});
