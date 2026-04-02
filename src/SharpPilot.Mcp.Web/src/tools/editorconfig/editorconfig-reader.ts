import { connect } from 'node:net';

export class EditorConfigReader {
    private readonly pipePath: string | undefined;

    constructor(pipeName?: string) {
        if (pipeName) {
            this.pipePath = process.platform === 'win32'
                ? `\\\\.\\pipe\\${pipeName}`
                : `/tmp/CoreFxPipe_${pipeName}`;
        }
    }

    /**
     * Resolves the effective `.editorconfig` properties for a given file path.
     * Returns a human-readable report.
     */
    async read(path: string): Promise<string> {
        if (!path.trim()) {
            throw new Error('File path must not be empty or whitespace.');
        }

        const properties = await this.query(path);
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
    async resolve(
        path: string | undefined,
        keys?: readonly string[],
    ): Promise<Record<string, string> | undefined> {
        if (!path?.trim()) {
            return undefined;
        }

        const properties = await this.query(path, keys);

        return Object.keys(properties).length === 0 ? undefined : properties;
    }

    /**
     * Sends a length-prefixed request to the workspace service over the named
     * pipe and returns the parsed properties.
     *
     * Protocol: 4-byte LE int32 length + UTF-8 JSON payload (both directions).
     */
    private query(filePath: string, keys?: readonly string[]): Promise<Record<string, string>> {
        if (!this.pipePath) {
            return Promise.resolve({});
        }

        const request = JSON.stringify(
            keys?.length
                ? { type: 'editorconfig', 'file-path': filePath, keys }
                : { type: 'editorconfig', 'file-path': filePath },
        );
        const requestBytes = Buffer.from(request, 'utf8');
        const header = Buffer.alloc(4);
        header.writeInt32LE(requestBytes.length, 0);

        const pipePath = this.pipePath;

        return new Promise((resolve) => {
            const chunks: Buffer[] = [];
            let received = 0;

            const socket = connect(pipePath, () => {
                socket.write(header);
                socket.write(requestBytes);
            });

            socket.on('data', (chunk: Buffer) => {
                chunks.push(chunk);
                received += chunk.length;

                // Need at least 4 bytes for the length header.
                if (received < 4) {
                    return;
                }

                // Read length from the first chunk if it's large enough,
                // otherwise concat once to get the header.
                const headerBuf = chunks[0]!.length >= 4
                    ? chunks[0]!
                    : Buffer.concat(chunks);

                const payloadLength = headerBuf.readInt32LE(0);

                // Need the full payload.
                if (received < 4 + payloadLength) {
                    return;
                }

                const buf = chunks.length === 1 ? chunks[0]! : Buffer.concat(chunks);
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
}
