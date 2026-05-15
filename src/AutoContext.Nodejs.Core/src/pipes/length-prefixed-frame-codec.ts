import type { Duplex } from 'node:stream';

/**
 * Wire-protocol adapter for AutoContext pipes: 4-byte little-endian
 * payload length followed by that many UTF-8 JSON bytes. Wraps a Node
 * {@link Duplex} stream (typically a `net.Socket`) and exposes
 * message-oriented read/write operations on top of it.
 *
 * This is the contract counterpart of the C# `LengthPrefixedFrameCodec`
 * in `AutoContext.Framework`; the two implementations are bit-for-bit
 * symmetric and must be changed together.
 *
 * The wrapped stream's lifetime is owned by the caller — this class
 * neither ends nor destroys it.
 */
export class LengthPrefixedFrameCodec {
    /**
     * Maximum payload size accepted by {@link LengthPrefixedFrameCodec.read}.
     * Caps allocation when a corrupted or hostile header arrives;
     * frames exchanged over AutoContext pipes are small JSON envelopes
     * well below this limit.
     */
    static readonly MAX_MESSAGE_BYTES = 64 * 1024 * 1024;

    private readonly stream: Duplex;

    constructor(stream: Duplex) {
        this.stream = stream;
    }

    /**
     * Reads one length-prefixed frame from the wrapped stream.
     * Returns `null` when the connection is closed before a full
     * header is received. Throws on header overflow or when
     * cancellation is signaled mid-read.
     */
    async read(signal?: AbortSignal): Promise<Buffer | null> {
        const header = await this.readExact(4, signal);

        if (header === null) {
            return null;
        }

        const length = header.readInt32LE(0);

        if (length < 0) {
            throw new Error(`Pipe message length ${length} is negative; header is corrupt.`);
        }

        if (length === 0) {
            return Buffer.alloc(0);
        }

        if (length > LengthPrefixedFrameCodec.MAX_MESSAGE_BYTES) {
            throw new Error(
                `Pipe message length ${length} exceeds the maximum of ${LengthPrefixedFrameCodec.MAX_MESSAGE_BYTES} bytes.`,
            );
        }

        return this.readExact(length, signal);
    }

    /**
     * Writes {@link payload} to the wrapped stream as one
     * length-prefixed frame.
     */
    async write(payload: Buffer, signal?: AbortSignal): Promise<void> {
        LengthPrefixedFrameCodec.throwIfAborted(signal);

        const message = Buffer.allocUnsafe(4 + payload.length);
        message.writeInt32LE(payload.length, 0);
        payload.copy(message, 4);

        const stream = this.stream;
        await new Promise<void>((resolve, reject) => {
            let settled = false;
            let abortListener: (() => void) | undefined;

            const settle = (action: () => void): void => {
                if (settled) {
                    return;
                }
                settled = true;
                if (abortListener !== undefined && signal !== undefined) {
                    signal.removeEventListener('abort', abortListener);
                }
                action();
            };

            if (signal !== undefined) {
                abortListener = (): void => {
                    settle(() => reject(LengthPrefixedFrameCodec.signalAbortError(signal)));
                };
                signal.addEventListener('abort', abortListener, { once: true });
            }

            stream.write(message, (err) => {
                if (err !== null && err !== undefined) {
                    settle(() => reject(err));
                    return;
                }
                settle(() => resolve());
            });
        });
    }

    private readExact(byteCount: number, signal: AbortSignal | undefined): Promise<Buffer | null> {
        const stream = this.stream;
        return new Promise((resolve, reject) => {
            if (signal?.aborted === true) {
                reject(LengthPrefixedFrameCodec.signalAbortError(signal));
                return;
            }

            if (byteCount === 0) {
                resolve(Buffer.alloc(0));
                return;
            }

            let settled = false;

            const settle = (action: () => void): void => {
                if (settled) {
                    return;
                }
                settled = true;
                stream.removeListener('readable', onReadable);
                stream.removeListener('end', onEnd);
                stream.removeListener('error', onError);
                if (signal !== undefined) {
                    signal.removeEventListener('abort', onAbort);
                }
                action();
            };

            const tryRead = (): void => {
                if (settled) {
                    return;
                }
                // `read(n)` returns exactly `n` bytes when available, or
                // null otherwise — avoiding the unshift/re-entry pitfalls
                // of the flowing 'data' mode. At EOF with fewer than `n`
                // bytes buffered, Node returns the partial remainder; we
                // treat that as a clean "stream ended" outcome.
                const chunk = stream.read(byteCount) as Buffer | null;
                if (chunk === null) {
                    return;
                }
                if (chunk.length < byteCount) {
                    settle(() => resolve(null));
                    return;
                }
                settle(() => resolve(chunk));
            };

            const onReadable = (): void => {
                tryRead();
            };

            const onEnd = (): void => {
                settle(() => resolve(null));
            };

            const onError = (err: Error): void => {
                settle(() => reject(err));
            };

            const onAbort = (): void => {
                settle(() => reject(LengthPrefixedFrameCodec.signalAbortError(signal!)));
            };

            stream.on('readable', onReadable);
            stream.on('end', onEnd);
            stream.on('error', onError);
            if (signal !== undefined) {
                signal.addEventListener('abort', onAbort, { once: true });
            }

            // Data may already be buffered (common for back-to-back reads
            // on the same stream); try once before relying on events.
            tryRead();
        });
    }

    private static throwIfAborted(signal: AbortSignal | undefined): void {
        if (signal?.aborted === true) {
            throw LengthPrefixedFrameCodec.signalAbortError(signal);
        }
    }

    private static signalAbortError(signal: AbortSignal): Error {
        const reason: unknown = signal.reason;
        if (reason instanceof Error) {
            return reason;
        }
        const err = new Error('The operation was aborted.');
        err.name = 'AbortError';
        return err;
    }
}
