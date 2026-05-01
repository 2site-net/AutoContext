import { parseArgs } from 'node:util';
import { WorkerTaskDispatcherService } from './workers/worker-task-dispatcher-service.js';
import { HealthMonitorClient } from './hosting/health-monitor-client.js';
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
 * Stable identifier this worker uses to announce itself to the
 * extension's `HealthMonitorServer`. Must match the `workerId`
 * referenced by the extension's MCP-tools manifest.
 */
const WORKER_ID = 'web';

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
            'instance-id': { type: 'string' },
            service: { type: 'string', multiple: true },
        },
        strict: false,
    });

    const instanceId = typeof values['instance-id'] === 'string' ? values['instance-id'].trim() : '';

    // Worker self-formats its listen address from its compile-time
    // WORKER_ID + the parsed --instance-id; the extension no longer
    // passes --pipe.
    const pipe = instanceId === ''
        ? `autocontext.worker-${WORKER_ID}`
        : `autocontext.worker-${WORKER_ID}#${instanceId}`;

    const services = new Map<string, string>();
    const rawServices = Array.isArray(values['service']) ? values['service'] as string[] : [];
    for (const entry of rawServices) {
        if (typeof entry !== 'string') { continue; }
        const sep = entry.indexOf('=');
        if (sep <= 0 || sep === entry.length - 1) {
            throw new Error(`'--service' value '${entry}' must be in '<role>=<address>' form.`);
        }
        const role = entry.slice(0, sep).trim();
        const address = entry.slice(sep + 1).trim();
        if (role === '' || address === '') {
            throw new Error(`'--service' value '${entry}' must have a non-empty role and address.`);
        }
        if (services.has(role)) {
            throw new Error(`'--service ${role}=...' was supplied more than once.`);
        }
        services.set(role, address);
    }

    const logPipe = services.get('log') ?? '';
    const healthMonitorPipe = services.get('health-monitor') ?? '';

    // Wire the logging client first so any startup errors below
    // also flow through the structured channel (or its stderr fallback
    // when --service log=... was not supplied).
    const loggingClient = new LoggingClient(logPipe, CLIENT_NAME);
    const logger = new PipeLogger(loggingClient);
    const serviceLogger = logger.forCategory('AutoContext.Framework.Workers.WorkerTaskDispatcherService');
    const healthLogger = logger.forCategory('AutoContext.Worker.Hosting.HealthMonitorClient');
    const startupLogger = logger.forCategory('AutoContext.Worker.Web.Startup');
    startupLogger.info(`Arguments parsed (pipe='${pipe}', logPipeEnabled=${logPipe !== ''}, healthMonitorEnabled=${healthMonitorPipe !== ''})`);

    const tasks: readonly McpTask[] = [
        new AnalyzeTypeScriptCodingStyleTask(),
    ];
    startupLogger.info(`Registered MCP tasks: ${tasks.map(t => t.taskName).join(', ')}`);

    const service = new WorkerTaskDispatcherService(
        { pipe, readyMarker: READY_MARKER, logPipe, clientName: CLIENT_NAME },
        tasks,
        serviceLogger,
    );

    const healthClient = new HealthMonitorClient(healthMonitorPipe, WORKER_ID, healthLogger);

    const controller = new AbortController();
    const shutdown = (): void => {
        if (!controller.signal.aborted) {
            controller.abort();
        }
    };

    process.once('SIGINT', shutdown);
    process.once('SIGTERM', shutdown);

    await service.start(controller.signal);
    await healthClient.start();

    try {
        // Block until the abort signal fires, then let WorkerTaskDispatcherService's own
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
        await healthClient.dispose();
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
