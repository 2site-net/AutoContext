import { describe, test, expect, beforeAll, afterAll } from 'vitest';
import { createServer, type Server } from 'node:net';
import { randomUUID } from 'node:crypto';
import { read, resolve, configurePipe } from '../src/editorconfig-reader.js';

/**
 * Minimal mock of the EditorConfig pipe service that uses the same
 * length-prefixed binary protocol as the real .NET service:
 * 4-byte LE int32 length + UTF-8 JSON payload (both directions).
 */
function createMockService(pipeName: string): Server {
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
            // Clear chunks for single-request-per-connection model.
            chunks.length = 0;

            try {
                const request = JSON.parse(payloadBytes.toString('utf8')) as { 'file-path'?: string; keys?: string[] };
                const filePath = request['file-path'] ?? '';

                // Simulate a small set of known responses.
                let properties: Record<string, string> = {};

                if (filePath.endsWith('.ts')) {
                    properties = { indent_style: 'space', indent_size: '2', charset: 'utf-8' };
                }

                if (request.keys?.length) {
                    const filtered: Record<string, string> = {};

                    for (const key of request.keys) {
                        if (key in properties) {
                            filtered[key] = properties[key];
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

describe('EditorConfigReader (pipe client)', () => {
    const pipeName = `ec-test-${randomUUID().replace(/-/g, '').slice(0, 12)}`;
    let mockServer: Server;

    beforeAll(async () => {
        mockServer = createMockService(pipeName);
        configurePipe(pipeName);

        await new Promise<void>((resolve) => {
            mockServer.once('listening', resolve);
        });
    });

    afterAll(async () => {
        await new Promise<void>((resolve, reject) => {
            mockServer.close((err) => (err ? reject(err) : resolve()));
        });
    });

    describe('read', () => {
        test('should return formatted properties for matching file', async () => {
            const result = await read('/repo/src/index.ts');

            expect(result).toContain('indent_style = space');
            expect(result).toContain('indent_size = 2');
        });

        test('should return warning for non-matching file', async () => {
            const result = await read('/repo/src/file.py');

            expect(result).toMatch(/^⚠️/);
            expect(result).toContain('No .editorconfig properties');
        });

        test('should throw on empty path', async () => {
            await expect(read('')).rejects.toThrow(Error);
            await expect(read('   ')).rejects.toThrow(Error);
        });
    });

    describe('resolve', () => {
        test('should return undefined when path is undefined', async () => {
            expect(await resolve(undefined)).toBeUndefined();
        });

        test('should return undefined when path is empty', async () => {
            expect(await resolve('')).toBeUndefined();
        });

        test('should return undefined when no properties apply', async () => {
            expect(await resolve('/repo/file.py')).toBeUndefined();
        });

        test('should return properties as a record', async () => {
            const result = await resolve('/repo/index.ts');

            expect(result).toBeDefined();
            expect(result!['indent_style']).toBe('space');
            expect(result!['indent_size']).toBe('2');
        });

        test('should filter by keys when provided', async () => {
            const result = await resolve('/repo/index.ts', ['indent_style']);

            expect(result).toBeDefined();
            expect(result!['indent_style']).toBe('space');
            expect(result!['indent_size']).toBeUndefined();
        });
    });
});
