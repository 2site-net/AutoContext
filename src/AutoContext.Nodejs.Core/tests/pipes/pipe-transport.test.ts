import { describe, it, expect } from 'vitest';
import { PipeTransport } from '#src/pipes/pipe-transport.js';
import { PipeListener } from '#src/pipes/pipe-listener.js';
import { FakeLoggerFactory } from '../support/logging/fake-logger-factory.js';
import { PipeNameTestFactory } from '../support/pipes/pipe-name-test-factory.js';

describe('PipeTransport', () => {
    it('rejects an empty pipe name with TypeError', async () => {
        const transport = new PipeTransport(FakeLoggerFactory.create());
        await expect(transport.connect('')).rejects.toBeInstanceOf(TypeError);
    });

    it('throws when the signal is already aborted', async () => {
        const transport = new PipeTransport(FakeLoggerFactory.create());
        const ac = new AbortController();
        ac.abort();
        await expect(transport.connect(PipeNameTestFactory.create(), ac.signal)).rejects.toThrow();
    });

    it('resolves to a writable socket when a server is listening', async () => {
        const name = PipeNameTestFactory.create();
        const bound = await new PipeListener(name, FakeLoggerFactory.create()).bind();
        const ac = new AbortController();
        const runTask = bound.run(async (socket) => {
            await new Promise<void>((resolve) => socket.once('close', () => resolve()));
        }, ac.signal);
        try {
            const transport = new PipeTransport(FakeLoggerFactory.create());
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
        const transport = new PipeTransport(FakeLoggerFactory.create());
        await expect(transport.connect(PipeNameTestFactory.create())).rejects.toThrow();
    });
});
