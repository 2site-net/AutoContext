import { createServer, type Server } from 'node:net';

/**
 * Minimal fake of the workspace service pipe server that uses the same
 * length-prefixed binary protocol as the real .NET service:
 * 4-byte LE int32 length + UTF-8 JSON payload (both directions).
 *
 * Responses are keyed by file path. If a file ends with `.ts`, default
 * properties are returned; otherwise the response is empty.
 */
export function createFakeWorkspaceServer(pipeName: string): Server {
    const pipePath = process.platform === 'win32'
        ? `\\\\.\\pipe\\${pipeName}`
        : `/tmp/CoreFxPipe_${pipeName}`;

    const server = createServer((socket) => {
        const chunks: Buffer[] = [];

        socket.on('data', (chunk: Buffer) => {
            chunks.push(chunk);

            const buf = Buffer.concat(chunks);

            if (buf.length < 4) {
                return;
            }

            const payloadLength = buf.readInt32LE(0);

            if (buf.length < 4 + payloadLength) {
                return;
            }

            const payloadBytes = buf.subarray(4, 4 + payloadLength);
            chunks.length = 0;

            try {
                const request = JSON.parse(payloadBytes.toString('utf8')) as {
                    'file-path'?: string;
                    keys?: string[];
                };
                const filePath = request['file-path'] ?? '';

                let properties: Record<string, string> = {};

                if (filePath.endsWith('.ts')) {
                    properties = { indent_style: 'space', indent_size: '2', charset: 'utf-8' };
                }

                if (request.keys?.length) {
                    const filtered: Record<string, string> = {};

                    for (const key of request.keys) {
                        if (key in properties) {
                            filtered[key] = properties[key]!;
                        }
                    }

                    properties = filtered;
                }

                const responseJson = JSON.stringify({ properties });
                const responseBytes = Buffer.from(responseJson, 'utf8');
                const header = Buffer.alloc(4);
                header.writeInt32LE(responseBytes.length, 0);
                socket.write(Buffer.concat([header, responseBytes]));
            } catch {
                const responseJson = JSON.stringify({ properties: {} });
                const responseBytes = Buffer.from(responseJson, 'utf8');
                const header = Buffer.alloc(4);
                header.writeInt32LE(responseBytes.length, 0);
                socket.write(Buffer.concat([header, responseBytes]));
            }
        });
    });

    server.listen(pipePath);

    return server;
}
