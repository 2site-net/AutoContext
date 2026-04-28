import { parseArgs } from 'node:util';
import { McpTaskDispatcherService } from './hosting/mcp-task-dispatcher-service.js';
import type { McpTask } from '#types/mcp-task.js';
import { LoggingClient } from './logging/logging-client.js';
import { PipeLogger } from './logging/logger.js';
import { AnalyzeTypeScriptCodingStyleTask } from './tasks/typescript/analyze-typescript-coding-style.js';

/**
 * Stderr ready-marker used by the parent (extension) process to detect
 * that this worker's pipe server is accepting connections.
 */
const READY_MARKER = '[AutoContext.Worker.Web] Ready.';

/**
 * Identifier sent on the LogServer greeting so the extension can
 * route this worker's records to a per-worker output channel.
 * Counterpart of `IHostEnvironment.ApplicationName` on the .NET side.
 */
const CLIENT_NAME = 'AutoContext.Worker.Web';

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
            'log-pipe': { type: 'string' },
        },
        strict: false,
    });

    const pipe = typeof values['pipe'] === 'string' ? values['pipe'] : undefined;
    if (pipe === undefined || pipe.trim() === '') {
        throw new Error('Missing required argument: --pipe');
    }

    const logPipe = typeof values['log-pipe'] === 'string' ? values['log-pipe'] : '';

    // Wire the logging client first so any startup errors below
    // also flow through the structured channel (or its stderr fallback
    // when --log-pipe was not supplied).
    const loggingClient = new LoggingClient(logPipe, CLIENT_NAME);
    const logger = new PipeLogger(loggingClient);
    const serviceLogger = logger.forCategory('AutoContext.Worker.Hosting.McpTaskDispatcherService');
    const startupLogger = logger.forCategory('AutoContext.Worker.Web.Startup');
    startupLogger.info(`Arguments parsed (pipe='${pipe}', logPipeEnabled=${logPipe !== ''})`);

    const tasks: readonly McpTask[] = [
        new AnalyzeTypeScriptCodingStyleTask(),
    ];
    startupLogger.info(`Registered MCP tasks: ${tasks.map(t => t.taskName).join(', ')}`);

    const service = new McpTaskDispatcherService(
        { pipe, readyMarker: READY_MARKER, logPipe, clientName: CLIENT_NAME },
        tasks,
        serviceLogger,
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
        // Block until the abort signal fires, then let McpTaskDispatcherService's own
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
        await loggingClient.dispose();
    }
}

main(process.argv.slice(2)).catch((err: unknown) => {
    const message = err instanceof Error ? err.message : String(err);
    // Direct stderr write here is intentional: this catch runs when
    // `main` failed before the logger could be wired (e.g. argument
    // parsing) or after `loggingClient.dispose()` already ran. We have
    // no logger to route through, so stderr is the only sink left.
    process.stderr.write(`[AutoContext.Worker.Web] Fatal: ${message}\n`);
    process.exitCode = 1;
});
