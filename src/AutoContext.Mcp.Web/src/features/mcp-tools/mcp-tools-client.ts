import { connect } from 'node:net';

export interface McpToolEntry {
    readonly name: string;
    readonly 'editorconfig-keys'?: readonly string[];
}

export interface McpToolResult {
    readonly name: string;
    readonly mode: 'run' | 'editorconfig-only' | 'skip';
    readonly data?: Record<string, string>;
}

export class McpToolsClient {
    private readonly pipePath: string | undefined;
    readonly workspacePath: string | undefined;

    constructor(pipeName?: string, workspacePath?: string) {
        this.workspacePath = workspacePath;

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
    async resolveTools(
        filePath: string | undefined,
        tools: readonly McpToolEntry[],
    ): Promise<McpToolResult[] | undefined> {
        if (!filePath?.trim() || !this.pipePath) {
            return undefined;
        }

        const response = await this.sendPipeRequest<{ 'mcp-tools'?: McpToolResult[] }>({
            type: 'mcp-tools',
            'file-path': filePath,
            'mcp-tools': tools,
        });

        const results = response['mcp-tools'];
        return results?.length ? results : undefined;
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
