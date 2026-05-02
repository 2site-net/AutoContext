import { describe, it, expect } from 'vitest';
import { connect, type Socket } from 'node:net';
import { PipeListener } from '#src/pipes/pipe-listener.js';
import { createFakeLogger, uniquePipeName, until } from './test-helpers.js';

function connectAsync(path: string): Promise<Socket> {
    return new Promise<Socket>((resolve, reject) => {
        const s = connect(path);
        const onError = (err: Error): void => {
            s.removeListener('connect', onConnect);
            reject(err);
        };
        const onConnect = (): void => {
            s.removeListener('error', onError);
            resolve(s);
        };
        s.once('connect', onConnect);
        s.once('error', onError);
    });
}

describe('PipeListener', () => {
    it('rejects empty pipe name in the constructor', () => {
        expect(() => new PipeListener('', createFakeLogger())).toThrow(TypeError);
    });

    it('binds successfully and exposes the listen path', async () => {
        const listener = new PipeListener(uniquePipeName(), createFakeLogger());
        const bound = await listener.bind();
        try {
            expect(bound.listenPath.length).toBeGreaterThan(0);
        }
        finally {
            await bound.dispose();
        }
    });

    it('rejects a second bind on the same listener', async () => {
        const listener = new PipeListener(uniquePipeName(), createFakeLogger());
        const bound = await listener.bind();
        try {
            await expect(listener.bind()).rejects.toThrow(/already been bound/);
        }
        finally {
            await bound.dispose();
        }
    });
});

describe('BoundPipeListener', () => {
    it('invokes the handler for every accepted connection', async () => {
        const listener = new PipeListener(uniquePipeName(), createFakeLogger());
        const bound = await listener.bind();
        const ac = new AbortController();
        let received = 0;
        const runTask = bound.run(async (socket) => {
            received += 1;
            await new Promise<void>((resolve) => socket.once('close', () => resolve()));
        }, ac.signal);
        try {
            const c1 = await connectAsync(bound.listenPath);
            c1.end();
            const c2 = await connectAsync(bound.listenPath);
            c2.end();
            await until(() => received === 2);
        }
        finally {
            ac.abort();
            await runTask;
            await bound.dispose();
        }
    });

    it('rejects a second call to run', async () => {
        const listener = new PipeListener(uniquePipeName(), createFakeLogger());
        const bound = await listener.bind();
        const ac = new AbortController();
        const first = bound.run(async () => { /* idle */ }, ac.signal);
        try {
            await expect(bound.run(async () => { /* idle */ }, ac.signal)).rejects.toThrow(/already been run/);
        }
        finally {
            ac.abort();
            await first;
            await bound.dispose();
        }
    });

    it('rejects run after dispose', async () => {
        const listener = new PipeListener(uniquePipeName(), createFakeLogger());
        const bound = await listener.bind();
        await bound.dispose();
        await expect(bound.run(async () => { /* idle */ }, new AbortController().signal))
            .rejects.toThrow(/disposed/);
    });

    it('returns from run only after in-flight handlers drain', async () => {
        const listener = new PipeListener(uniquePipeName(), createFakeLogger());
        const bound = await listener.bind();
        const ac = new AbortController();
        let started = false;
        let finished = false;
        const runTask = bound.run(async () => {
            started = true;
            await new Promise<void>((resolve) => setTimeout(resolve, 50));
            finished = true;
        }, ac.signal);
        try {
            const client = await connectAsync(bound.listenPath);
            await until(() => started);
            ac.abort();
            await runTask;
            expect(finished).toBe(true);
            client.destroy();
        }
        finally {
            await bound.dispose();
        }
    });

    it('returns immediately from run when the signal is already aborted', async () => {
        const listener = new PipeListener(uniquePipeName(), createFakeLogger());
        const bound = await listener.bind();
        const ac = new AbortController();
        ac.abort();
        let handlerCalls = 0;
        await bound.run(async () => { handlerCalls += 1; }, ac.signal);
        expect(handlerCalls).toBe(0);
        await bound.dispose();
    });

    it('logs and recovers when a handler throws', async () => {
        const logger = createFakeLogger();
        const listener = new PipeListener(uniquePipeName(), logger);
        const bound = await listener.bind();
        const ac = new AbortController();
        let calls = 0;
        const runTask = bound.run(async () => {
            calls += 1;
            if (calls === 1) {
                throw new Error('boom');
            }
        }, ac.signal);
        try {
            const c1 = await connectAsync(bound.listenPath);
            await until(() => logger.logs.some((l) => l.level === 'error'));
            c1.destroy();
            const c2 = await connectAsync(bound.listenPath);
            await until(() => calls === 2);
            c2.destroy();
        }
        finally {
            ac.abort();
            await runTask;
            await bound.dispose();
        }
    });

    it('dispose is idempotent', async () => {
        const listener = new PipeListener(uniquePipeName(), createFakeLogger());
        const bound = await listener.bind();
        await expect(bound.dispose()).resolves.toBeUndefined();
        await expect(bound.dispose()).resolves.toBeUndefined();
        await expect(bound.dispose()).resolves.toBeUndefined();
    });
});
