import { parseArgs } from 'node:util';
import { McpToolService } from './hosting/mcp-tool-service.js';
import type { McpTask } from './hosting/mcp-task.js';
import { AnalyzeTypeScriptCodingStyleTask } from './tasks/typescript/analyze-typescript-coding-style.js';

/**
 * Stderr ready-marker used by the parent (extension) process to detect
 * that this worker's pipe server is accepting connections.
 */
const READY_MARKER = '[AutoContext.Worker.Web] Ready.';

/**
 * AutoContext.Worker.Web entry point. Standalone Node.js process that
 * owns the Web-side MCP Tasks (TypeScript coding-style checks) and
 * serves them over a named pipe. Conceptually the Node sibling of
 * `AutoContext.Worker.DotNet`.
 */
async function main(argv: readonly string[]): Promise<void> {
    const { values } = parseArgs({
        args: [...argv],
        options: {
            pipe: { type: 'string' },
        },
        strict: false,
    });

    const pipe = typeof values['pipe'] === 'string' ? values['pipe'] : undefined;
    if (pipe === undefined || pipe.trim() === '') {
        throw new Error('Missing required argument: --pipe');
    }

    const tasks: readonly McpTask[] = [
        new AnalyzeTypeScriptCodingStyleTask(),
    ];

    const service = new McpToolService(
        { pipe, readyMarker: READY_MARKER },
        tasks,
    );

    const controller = new AbortController();
    const shutdown = (): void => {
        if (!controller.signal.aborted) {
            controller.abort();
        }
    };

    process.once('SIGINT', shutdown);
    process.once('SIGTERM', shutdown);

    await service.start(controller.signal);

    try {
        // Block until the abort signal fires, then let McpToolService's own
        // abort-listener drain in-flight handlers and close the server.
        await new Promise<void>((resolve) => {
            if (controller.signal.aborted) {
                resolve();
                return;
            }
            controller.signal.addEventListener('abort', () => resolve(), { once: true });
        });
    } finally {
        // Always release the pipe server, even if the wait above throws.
        await service.stop();
    }
}

main(process.argv.slice(2)).catch((err: unknown) => {
    const message = err instanceof Error ? err.message : String(err);
    process.stderr.write(`[AutoContext.Worker.Web] Fatal: ${message}\n`);
    process.exitCode = 1;
});
