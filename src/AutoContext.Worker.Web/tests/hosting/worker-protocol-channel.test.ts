import { describe, it, expect } from 'vitest';
import { PassThrough } from 'node:stream';
import { WorkerProtocolChannel } from '../../src/hosting/worker-protocol-channel.js';

function frame(payload: Buffer): Buffer {
    const header = Buffer.allocUnsafe(4);
    header.writeInt32LE(payload.length, 0);
    return Buffer.concat([header, payload]);
}

describe('WorkerProtocolChannel', () => {
    it('round-trips a single message', async () => {
        const stream = new PassThrough();
        const channel = new WorkerProtocolChannel(stream);
        const payload = Buffer.from('{"mcpTask":"test"}', 'utf8');

        await channel.write(payload);
        const result = await channel.read();

        expect(result).not.toBeNull();
        expect(result!.toString('utf8')).toBe('{"mcpTask":"test"}');
    });

    it('returns null when stream ends before any bytes arrive', async () => {
        const stream = new PassThrough();
        const channel = new WorkerProtocolChannel(stream);
        stream.end();

        const result = await channel.read();

        expect(result).toBeNull();
    });

    it('returns null when stream ends mid-header', async () => {
        const stream = new PassThrough();
        const channel = new WorkerProtocolChannel(stream);
        stream.write(Buffer.from([0x10, 0x00]));
        stream.end();

        const result = await channel.read();

        expect(result).toBeNull();
    });

    it('returns null when stream ends mid-payload', async () => {
        const stream = new PassThrough();
        const channel = new WorkerProtocolChannel(stream);
        const header = Buffer.allocUnsafe(4);
        header.writeInt32LE(10, 0);
        stream.write(header);
        stream.write(Buffer.from('abc'));
        stream.end();

        const result = await channel.read();

        expect(result).toBeNull();
    });

    it('returns an empty buffer for zero-length payloads', async () => {
        const stream = new PassThrough();
        const channel = new WorkerProtocolChannel(stream);
        const header = Buffer.allocUnsafe(4);
        header.writeInt32LE(0, 0);
        stream.write(header);

        const result = await channel.read();

        expect(result).not.toBeNull();
        expect(result!.length).toBe(0);
    });

    it('throws when the announced length exceeds MAX_MESSAGE_BYTES', async () => {
        const stream = new PassThrough();
        const channel = new WorkerProtocolChannel(stream);
        const header = Buffer.allocUnsafe(4);
        header.writeInt32LE(WorkerProtocolChannel.MAX_MESSAGE_BYTES + 1, 0);
        stream.write(header);

        await expect(channel.read()).rejects.toThrow(/exceeds the maximum/);
    });

    it('throws when the announced length is negative', async () => {
        const stream = new PassThrough();
        const channel = new WorkerProtocolChannel(stream);
        const header = Buffer.allocUnsafe(4);
        header.writeInt32LE(-1, 0);
        stream.write(header);

        await expect(channel.read()).rejects.toThrow(/negative/);
    });

    it('reads two back-to-back messages from the same stream', async () => {
        const stream = new PassThrough();
        const channel = new WorkerProtocolChannel(stream);
        const first = Buffer.from('first', 'utf8');
        const second = Buffer.from('second-longer', 'utf8');
        stream.write(Buffer.concat([frame(first), frame(second)]));

        const r1 = await channel.read();
        const r2 = await channel.read();

        expect(r1!.toString('utf8')).toBe('first');
        expect(r2!.toString('utf8')).toBe('second-longer');
    });

    it('rejects the read when the signal is aborted mid-read', async () => {
        const stream = new PassThrough();
        const channel = new WorkerProtocolChannel(stream);
        const controller = new AbortController();

        const header = Buffer.allocUnsafe(4);
        header.writeInt32LE(100, 0);
        stream.write(header);

        const pending = channel.read(controller.signal);
        controller.abort();

        await expect(pending).rejects.toThrow();
    });
});
