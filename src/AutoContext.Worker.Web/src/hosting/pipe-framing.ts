import type { Readable, Writable } from 'node:stream';

/**
 * Maximum payload size accepted by {@link readMessage}. Caps allocation
 * when a corrupted or hostile header arrives; tasks exchanged on this
 * pipe are small JSON envelopes well below this limit.
 */
export const MAX_MESSAGE_BYTES = 64 * 1024 * 1024;

/**
 * Length-prefixed binary framing helpers for the worker pipe protocol:
 * 4-byte little-endian payload length followed by that many UTF-8 JSON
 * bytes. Matches the C# `PipeFraming` helper in
 * `AutoContext.Worker.Shared` bit-for-bit.
 */

/**
 * Reads one length-prefixed message from {@link stream}. Returns
 * `null` when the connection is closed before a full header is
 * received. Throws on header overflow or when cancellation is signaled
 * mid-read.
 */
export async function readMessage(stream: Readable, signal?: AbortSignal): Promise<Buffer | null> {
    const header = await readExact(stream, 4, signal);

    if (header === null) {
        return null;
    }

    const length = header.readInt32LE(0);

    if (length <= 0) {
        return Buffer.alloc(0);
    }

    if (length > MAX_MESSAGE_BYTES) {
        throw new Error(
            `Pipe message length ${length} exceeds the maximum of ${MAX_MESSAGE_BYTES} bytes.`,
        );
    }

    return readExact(stream, length, signal);
}

/**
 * Writes {@link payload} as one length-prefixed message on
 * {@link stream}.
 */
export async function writeMessage(
    stream: Writable,
    payload: Buffer,
    signal?: AbortSignal,
): Promise<void> {
    throwIfAborted(signal);

    const message = Buffer.allocUnsafe(4 + payload.length);
    message.writeInt32LE(payload.length, 0);
    payload.copy(message, 4);

    await new Promise<void>((resolve, reject) => {
        let abortListener: (() => void) | undefined;

        const cleanup = (): void => {
            if (abortListener !== undefined) {
                signal?.removeEventListener('abort', abortListener);
            }
        };

        if (signal !== undefined) {
            abortListener = (): void => {
                cleanup();
                reject(signalAbortError(signal));
            };
            signal.addEventListener('abort', abortListener, { once: true });
        }

        stream.write(message, (err) => {
            cleanup();
            if (err !== null && err !== undefined) {
                reject(err);
                return;
            }
            resolve();
        });
    });
}

function readExact(
    stream: Readable,
    byteCount: number,
    signal: AbortSignal | undefined,
): Promise<Buffer | null> {
    return new Promise((resolve, reject) => {
        if (signal?.aborted === true) {
            reject(signalAbortError(signal));
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
            settle(() => reject(signalAbortError(signal!)));
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

function throwIfAborted(signal: AbortSignal | undefined): void {
    if (signal?.aborted === true) {
        throw signalAbortError(signal);
    }
}

function signalAbortError(signal: AbortSignal): Error {
    const reason: unknown = signal.reason;
    if (reason instanceof Error) {
        return reason;
    }
    const err = new Error('The operation was aborted.');
    err.name = 'AbortError';
    return err;
}
