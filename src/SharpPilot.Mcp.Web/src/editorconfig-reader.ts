import { connect } from 'node:net';

let pipeName: string | undefined;

/**
 * Configures the pipe name used to connect to the EditorConfig service.
 */
export function configurePipe(name: string): void {
    pipeName = name;
}

/**
 * Sends a length-prefixed request to the EditorConfig service over the named
 * pipe and returns the parsed properties.
 *
 * Protocol: 4-byte LE int32 length + UTF-8 JSON payload (both directions).
 */
function queryAsync(filePath: string, keys?: readonly string[]): Promise<Record<string, string>> {
    if (!pipeName) {
        return Promise.resolve({});
    }

    const request = JSON.stringify(keys?.length ? { filePath, keys } : { filePath });
    const requestBytes = Buffer.from(request, 'utf8');
    const header = Buffer.alloc(4);
    header.writeInt32LE(requestBytes.length, 0);

    const pipePath = process.platform === 'win32'
        ? `\\\\.\\pipe\\${pipeName}`
        : `/tmp/CoreFxPipe_${pipeName}`;

    return new Promise((resolve) => {
        const chunks: Buffer[] = [];

        const socket = connect(pipePath, () => {
            socket.write(Buffer.concat([header, requestBytes]));
        });

        socket.on('data', (chunk: Buffer) => {
            chunks.push(chunk);

            const buf = Buffer.concat(chunks);

            // Need at least 4 bytes for the length header.
            if (buf.length < 4) {
                return;
            }

            const payloadLength = buf.readInt32LE(0);

            // Need the full payload.
            if (buf.length < 4 + payloadLength) {
                return;
            }

            const payloadBytes = buf.subarray(4, 4 + payloadLength);
            socket.destroy();

            try {
                const response = JSON.parse(payloadBytes.toString('utf8')) as { properties?: Record<string, string> };
                resolve(response.properties ?? {});
            } catch {
                resolve({});
            }
        });

        socket.on('error', () => resolve({}));
        socket.on('timeout', () => { socket.destroy(); resolve({}); });
        socket.setTimeout(5000);
    });
}

/**
 * Resolves the effective `.editorconfig` properties for a given file path.
 * Walks up the directory tree, evaluates glob patterns and section cascading,
 * and returns the final resolved key-value pairs formatted as a human-readable
 * report.
 */
export async function read(path: string): Promise<string> {
    if (!path.trim()) {
        throw new Error('File path must not be empty or whitespace.');
    }

    const properties = await queryAsync(path);
    const entries = Object.entries(properties);

    if (entries.length === 0) {
        return '⚠️ No .editorconfig properties apply to this file.';
    }

    return entries.map(([key, value]) => `${key} = ${String(value)}`).join('\n');
}

/**
 * Resolves the effective `.editorconfig` properties for a given file path
 * as a record for programmatic use by checkers.
 *
 * Returns `undefined` when the path is empty or no properties apply.
 */
export async function resolve(
    path: string | undefined,
    keys?: readonly string[],
): Promise<Record<string, string> | undefined> {
    if (!path?.trim()) {
        return undefined;
    }

    const properties = await queryAsync(path, keys);

    return Object.keys(properties).length === 0 ? undefined : properties;
}
