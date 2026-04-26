import * as vscode from 'vscode';
import { LogLevel, type LogCategory, type Logger } from './types/logger.js';

/**
 * Default {@link Logger} implementation backed by a single
 * {@link vscode.OutputChannel}.  Lines are written with a
 * `[level] [Category] message[: error]` format so a future switch to
 * `LogOutputChannel` (which renders its own level + timestamp) can
 * drop the manual `[level]` prefix without changing call sites.
 *
 * The output channel is supplied by the caller and **not** disposed
 * here — its lifetime belongs to whoever created it (typically
 * `extension.ts` via `context.subscriptions`).
 */
export class OutputChannelLogger implements Logger {
    private static readonly LEVEL_LABEL: Record<Exclude<LogLevel, typeof LogLevel.Off>, string> = {
        [LogLevel.Trace]: 'trace',
        [LogLevel.Debug]: 'debug',
        [LogLevel.Info]: 'info',
        [LogLevel.Warn]: 'warn',
        [LogLevel.Error]: 'error',
    };

    constructor(
        private readonly outputChannel: vscode.OutputChannel,
        private readonly category: string | undefined = undefined,
        private readonly minLevel: LogLevel = LogLevel.Info,
    ) {}

    trace(message: string, error?: unknown): void { this.write(LogLevel.Trace, message, error); }
    debug(message: string, error?: unknown): void { this.write(LogLevel.Debug, message, error); }
    info(message: string, error?: unknown): void { this.write(LogLevel.Info, message, error); }
    warn(message: string, error?: unknown): void { this.write(LogLevel.Warn, message, error); }
    error(message: string, error?: unknown): void { this.write(LogLevel.Error, message, error); }

    forCategory(category: LogCategory | string): Logger {
        return new OutputChannelLogger(this.outputChannel, category, this.minLevel);
    }

    private write(level: LogLevel, message: string, error?: unknown): void {
        if (level < this.minLevel) {
            return;
        }
        const label = OutputChannelLogger.LEVEL_LABEL[level as Exclude<LogLevel, typeof LogLevel.Off>];
        const prefix = this.category === undefined ? '' : `[${this.category}] `;
        const tail = error === undefined ? '' : `: ${OutputChannelLogger.formatError(error)}`;
        this.outputChannel.appendLine(`[${label}] ${prefix}${message}${tail}`);
    }

    /**
     * Renders an arbitrary error value the same way every site used to.
     * `Error` instances expose their stack (or message when no stack is
     * available); anything else is coerced via `String(...)`.
     */
    private static formatError(error: unknown): string {
        if (error instanceof Error) {
            return error.stack ?? error.message;
        }

        return String(error);
    }
}
