import * as vscode from 'vscode';
import { LogLevel, type LogCategory, type Logger } from './types/logger.js';

/**
 * Default {@link Logger} implementation backed by a
 * {@link vscode.OutputChannel}. Lines are written with a
 * `[level] [Category] message[: error]` format so a future switch to
 * `LogOutputChannel` (which renders its own level + timestamp) can
 * drop the manual `[level]` prefix without changing call sites.
 *
 * A single channel registry is shared across the entire parent/child
 * logger tree: calling {@link forChannel} from anywhere returns a
 * logger backed by the same cached {@link vscode.OutputChannel}, and
 * {@link dispose} on the root tears down every channel that was
 * created through it.
 */
export class OutputChannelLogger implements Logger, vscode.Disposable {
    private static readonly LEVEL_LABEL: Record<Exclude<LogLevel, typeof LogLevel.Off>, string> = {
        [LogLevel.Trace]: 'trace',
        [LogLevel.Debug]: 'debug',
        [LogLevel.Info]: 'info',
        [LogLevel.Warn]: 'warn',
        [LogLevel.Error]: 'error',
    };

    private readonly channels: Map<string, vscode.OutputChannel>;
    private readonly createChannel: (name: string) => vscode.OutputChannel;

    constructor(
        private readonly outputChannel: vscode.OutputChannel,
        private readonly category: string | undefined = undefined,
        private readonly minLevel: LogLevel = LogLevel.Info,
        channels?: Map<string, vscode.OutputChannel>,
        createChannel?: (name: string) => vscode.OutputChannel,
    ) {
        this.createChannel = createChannel ?? ((name) => vscode.window.createOutputChannel(name));
        this.channels = channels ?? new Map([[outputChannel.name, outputChannel]]);
    }

    /**
     * Convenience factory: creates the root output channel and a
     * matching logger in one call. The returned logger is the sole
     * owner of the channel — push it into `context.subscriptions`
     * directly instead of the channel.
     */
    static create(rootChannelName: string, minLevel: LogLevel = LogLevel.Info): OutputChannelLogger {
        const root = vscode.window.createOutputChannel(rootChannelName);
        return new OutputChannelLogger(root, undefined, minLevel);
    }

    trace(message: string, error?: unknown): void { this.write(LogLevel.Trace, message, error); }
    debug(message: string, error?: unknown): void { this.write(LogLevel.Debug, message, error); }
    info(message: string, error?: unknown): void { this.write(LogLevel.Info, message, error); }
    warn(message: string, error?: unknown): void { this.write(LogLevel.Warn, message, error); }
    error(message: string, error?: unknown): void { this.write(LogLevel.Error, message, error); }

    forCategory(category: LogCategory | string): Logger {
        return new OutputChannelLogger(this.outputChannel, category, this.minLevel, this.channels, this.createChannel);
    }

    forChannel(name: string): Logger {
        let channel = this.channels.get(name);
        if (channel === undefined) {
            channel = this.createChannel(name);
            this.channels.set(name, channel);
        }
        return new OutputChannelLogger(channel, undefined, this.minLevel, this.channels, this.createChannel);
    }

    clear(): void {
        this.outputChannel.clear();
    }

    /**
     * Disposes every output channel this logger has created (including
     * the root channel passed at construction). Safe to call from any
     * logger in the tree because the registry is shared, but in
     * practice only the root logger should be registered with
     * `context.subscriptions`.
     */
    dispose(): void {
        for (const channel of this.channels.values()) {
            channel.dispose();
        }
        this.channels.clear();
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
