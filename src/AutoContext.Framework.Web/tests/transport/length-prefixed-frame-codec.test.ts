import { describe, it, expect } from 'vitest';
import { PassThrough } from 'node:stream';
import { LengthPrefixedFrameCodec } from '#src/transport/length-prefixed-frame-codec.js';

function frame(payload: Buffer): Buffer {
    const header = Buffer.allocUnsafe(4);
    header.writeInt32LE(payload.length, 0);
    return Buffer.concat([header, payload]);
}

describe('LengthPrefixedFrameCodec', () => {
    it('round-trips a single message', async () => {
        const stream = new PassThrough();
        const codec = new LengthPrefixedFrameCodec(stream);
        const payload = Buffer.from('{"mcpTask":"test"}', 'utf8');

        await codec.write(payload);
        const result = await codec.read();

        expect(result).not.toBeNull();
        expect(result!.toString('utf8')).toBe('{"mcpTask":"test"}');
    });

    it('returns null when stream ends before any bytes arrive', async () => {
        const stream = new PassThrough();
        const codec = new LengthPrefixedFrameCodec(stream);
        stream.end();

        const result = await codec.read();

        expect(result).toBeNull();
    });

    it('returns null when stream ends mid-header', async () => {
        const stream = new PassThrough();
        const codec = new LengthPrefixedFrameCodec(stream);
        stream.write(Buffer.from([0x10, 0x00]));
        stream.end();

        const result = await codec.read();

        expect(result).toBeNull();
    });

    it('returns null when stream ends mid-payload', async () => {
        const stream = new PassThrough();
        const codec = new LengthPrefixedFrameCodec(stream);
        const header = Buffer.allocUnsafe(4);
        header.writeInt32LE(10, 0);
        stream.write(header);
        stream.write(Buffer.from('abc'));
        stream.end();

        const result = await codec.read();

        expect(result).toBeNull();
    });

    it('returns an empty buffer for zero-length payloads', async () => {
        const stream = new PassThrough();
        const codec = new LengthPrefixedFrameCodec(stream);
        const header = Buffer.allocUnsafe(4);
        header.writeInt32LE(0, 0);
        stream.write(header);

        const result = await codec.read();

        expect(result).not.toBeNull();
        expect(result!.length).toBe(0);
    });

    it('throws when the announced length exceeds MAX_MESSAGE_BYTES', async () => {
        const stream = new PassThrough();
        const codec = new LengthPrefixedFrameCodec(stream);
        const header = Buffer.allocUnsafe(4);
        header.writeInt32LE(LengthPrefixedFrameCodec.MAX_MESSAGE_BYTES + 1, 0);
        stream.write(header);

        await expect(codec.read()).rejects.toThrow(/exceeds the maximum/);
    });

    it('throws when the announced length is negative', async () => {
        const stream = new PassThrough();
        const codec = new LengthPrefixedFrameCodec(stream);
        const header = Buffer.allocUnsafe(4);
        header.writeInt32LE(-1, 0);
        stream.write(header);

        await expect(codec.read()).rejects.toThrow(/negative/);
    });

    it('reads two back-to-back messages from the same stream', async () => {
        const stream = new PassThrough();
        const codec = new LengthPrefixedFrameCodec(stream);
        const first = Buffer.from('first', 'utf8');
        const second = Buffer.from('second-longer', 'utf8');
        stream.write(Buffer.concat([frame(first), frame(second)]));

        const r1 = await codec.read();
        const r2 = await codec.read();

        expect(r1!.toString('utf8')).toBe('first');
        expect(r2!.toString('utf8')).toBe('second-longer');
    });

    it('rejects the read when the signal is aborted mid-read', async () => {
        const stream = new PassThrough();
        const codec = new LengthPrefixedFrameCodec(stream);
        const controller = new AbortController();

        const header = Buffer.allocUnsafe(4);
        header.writeInt32LE(100, 0);
        stream.write(header);

        const pending = codec.read(controller.signal);
        controller.abort();

        await expect(pending).rejects.toThrow();
    });

    it('rejects the write when the signal is already aborted', async () => {
        const stream = new PassThrough();
        const codec = new LengthPrefixedFrameCodec(stream);
        const controller = new AbortController();
        controller.abort();

        await expect(codec.write(Buffer.from('x'), controller.signal)).rejects.toThrow();
    });

    it('settles the write exactly once when abort and write complete race', async () => {
        // Drain the stream synchronously so the write callback fires
        // immediately, while we abort the signal in the same tick.
        const stream = new PassThrough();
        stream.resume();
        const codec = new LengthPrefixedFrameCodec(stream);
        const controller = new AbortController();

        const pending = codec.write(Buffer.from('payload'), controller.signal);
        controller.abort();

        // Whichever arm wins the race, the promise must settle and not
        // emit an unhandled rejection from a second settle attempt.
        await pending.catch(() => {});
        // Give the event loop a tick to flush any stray settle callbacks.
        await new Promise((resolve) => setImmediate(resolve));
    });
});
