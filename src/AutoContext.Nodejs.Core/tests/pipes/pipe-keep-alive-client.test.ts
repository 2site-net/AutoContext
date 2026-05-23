import { describe, it, expect } from 'vitest';
import { PipeKeepAliveClient } from '#src/pipes/pipe-keep-alive-client.js';
import { PipeTransport } from '#src/pipes/pipe-transport.js';
import { PipeListener } from '#src/pipes/pipe-listener.js';
import { FakeLoggerFactory } from '../support/logging/fake-logger-factory.js';
import { PipeNameTestFactory } from '../support/pipes/pipe-name-test-factory.js';
import { PromiseTestUtils } from '../support/shared/promise-test-utils.js';

describe('PipeKeepAliveClient', () => {
    it('is a no-op when the pipe name is empty', async () => {
        const logger = FakeLoggerFactory.create();
        const client = new PipeKeepAliveClient(new PipeTransport(FakeLoggerFactory.create()), logger);
        await client.start('', Buffer.from('hi'));
        expect(logger.logs.some((l) => l.message.includes('not configured'))).toBe(true);
        await client.dispose();
    });

    it('writes the handshake to the listener', async () => {
        const name = PipeNameTestFactory.create();
        const captured: Buffer[] = [];
        const bound = await new PipeListener(name, FakeLoggerFactory.create()).bind();
        const ac = new AbortController();
        const runTask = bound.run(async (socket) => {
            socket.on('data', (chunk: Buffer) => captured.push(chunk));
            await new Promise<void>((resolve) => socket.once('close', () => resolve()));
        }, ac.signal);
        const client = new PipeKeepAliveClient(new PipeTransport(FakeLoggerFactory.create()), FakeLoggerFactory.create());
        try {
            await client.start(name, Buffer.from('HEY', 'utf8'));
            await PromiseTestUtils.until(() => Buffer.concat(captured).toString('utf8') === 'HEY');
        }
        finally {
            await client.dispose();
            ac.abort();
            await runTask;
            await bound.dispose();
        }
    });

    it('connects without sending data when the handshake is empty', async () => {
        const name = PipeNameTestFactory.create();
        const captured: Buffer[] = [];
        const bound = await new PipeListener(name, FakeLoggerFactory.create()).bind();
        const ac = new AbortController();
        let connected = false;
        const runTask = bound.run(async (socket) => {
            connected = true;
            socket.on('data', (chunk: Buffer) => captured.push(chunk));
            await new Promise<void>((resolve) => socket.once('close', () => resolve()));
        }, ac.signal);
        const client = new PipeKeepAliveClient(new PipeTransport(FakeLoggerFactory.create()), FakeLoggerFactory.create());
        try {
            await client.start(name, Buffer.alloc(0));
            await PromiseTestUtils.until(() => connected);
            expect(captured.length).toBe(0);
        }
        finally {
            await client.dispose();
            ac.abort();
            await runTask;
            await bound.dispose();
        }
    });

    it('swallows connect failures and logs at warn', async () => {
        const logger = FakeLoggerFactory.create();
        const client = new PipeKeepAliveClient(new PipeTransport(FakeLoggerFactory.create()), logger);
        await client.start(PipeNameTestFactory.create(), Buffer.from('x'), 100);
        expect(logger.logs.some((l) => l.level === 'warn')).toBe(true);
        await client.dispose();
    });

    it('start is idempotent and shares a single completion promise', async () => {
        const client = new PipeKeepAliveClient(new PipeTransport(FakeLoggerFactory.create()), FakeLoggerFactory.create());
        const first = client.start('', Buffer.alloc(0));
        const second = client.start('', Buffer.alloc(0));
        expect(first).toBe(second);
        await first;
        await client.dispose();
    });

    it('dispose tears down the held socket and is idempotent', async () => {
        const name = PipeNameTestFactory.create();
        const bound = await new PipeListener(name, FakeLoggerFactory.create()).bind();
        const ac = new AbortController();
        const runTask = bound.run(async (socket) => {
            await new Promise<void>((resolve) => socket.once('close', () => resolve()));
        }, ac.signal);
        const client = new PipeKeepAliveClient(new PipeTransport(FakeLoggerFactory.create()), FakeLoggerFactory.create());
        await client.start(name, Buffer.from('h'));
        try {
            await client.dispose();
            const second = client.dispose();
            await expect(second).resolves.toBeUndefined();
        }
        finally {
            ac.abort();
            await runTask;
            await bound.dispose();
        }
    });
});
