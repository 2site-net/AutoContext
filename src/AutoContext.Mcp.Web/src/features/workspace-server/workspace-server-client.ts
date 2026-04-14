import { connect } from 'node:net';

import type { McpToolsRequest } from './mcp-tools/mcp-tools-request.js';
import type { McpToolsResponse } from './mcp-tools/mcp-tools-response.js';
import type { LogRequest } from './logging/log-request.js';

export class WorkspaceServerClient {
    private readonly pipePath: string | undefined;
    private readonly source: string;

    constructor(pipeName?: string, source?: string) {
        this.source = source ?? 'Unknown';

        if (pipeName) {
            this.pipePath = process.platform === 'win32'
                ? `\\\\.\\pipe\\${pipeName}`
                : `/tmp/CoreFxPipe_${pipeName}`;
        }
    }

    /**
     * Resolves tool modes and EditorConfig data for a batch of MCP tools
     * via the `mcp-tools` workspace service endpoint.
     */
    async resolveTools(request: McpToolsRequest): Promise<McpToolsResponse | undefined> {
        if (!this.pipePath) {
            return undefined;
        }

        const response = await this.sendPipeRequest<McpToolsResponse>({
            type: 'mcp-tools',
            tools: request.tools,
            'file-path': request.filePath,
            'editorconfig-keys': request['editorconfig-keys'],
        });

        return response.tools ? response : undefined;
    }

    /**
     * Sends a log message to the workspace service for centralized output.
     * Falls back to local stderr when the pipe is unavailable.
     */
    async sendLog(level: string, message: string): Promise<void> {
        if (!this.pipePath) {
            // No pipe configured — write directly to stderr (expected when running standalone).
            process.stderr.write(`[${this.source}] ${message}\n`);
            return;
        }

        try {
            const request: LogRequest = {
                type: 'log',
                source: this.source,
                level,
                message,
            };
            await this.sendPipeRequest(request);
        } catch (ex) {
            // Pipe failed — log the reason and the original message to local stderr.
            const reason = ex instanceof Error ? ex.message : String(ex);
            process.stderr.write(`[${this.source}] Failed to send log via workspace server: ${reason}\n`);
            process.stderr.write(`[${this.source}] ${message}\n`);
        }
    }

    /**
     * Low-level pipe protocol: sends a length-prefixed JSON request and
     * returns the parsed response object.
     */
    private sendPipeRequest<T>(request: object): Promise<T> {
        if (!this.pipePath) {
            return Promise.resolve({} as T);
        }

        const json = JSON.stringify(request);
        const requestBytes = Buffer.from(json, 'utf8');
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
                    const response = JSON.parse(payloadBytes.toString('utf8')) as T;
                    resolve(response);
                } catch {
                    resolve({} as T);
                }
            });

            socket.on('error', () => resolve({} as T));
            socket.on('timeout', () => { socket.destroy(); resolve({} as T); });
            socket.setTimeout(5000);
        });
    }
}
