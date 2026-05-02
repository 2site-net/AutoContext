import { LoggerBase, LogLevel, type ChannelLogger, type LogCategory } from 'autocontext-framework-web';
import * as vscode from 'vscode';

/**
 * Default {@link ChannelLogger} implementation backed by a
 * {@link vscode.LogOutputChannel}. The channel renders the level and
 * an ISO timestamp natively, and exposes a built-in
 * `right-click → "Set Log Level…"` menu in the Output panel for
 * per-channel user filtering — so this class only has to forward the
 * call to the matching `trace/debug/info/warn/error` method and stitch
 * on a `[Category] ` prefix when one is configured.
 *
 * A single channel registry is shared across the entire parent/child
 * logger tree: calling {@link forChannel} from anywhere returns a
 * logger backed by the same cached {@link vscode.LogOutputChannel},
 * and {@link dispose} on the root tears down every channel that was
 * created through it.
 */
export class OutputChannelLogger extends LoggerBase implements ChannelLogger, vscode.Disposable {
    private readonly channels: Map<string, vscode.LogOutputChannel>;
    private readonly createChannel: (name: string) => vscode.LogOutputChannel;

    constructor(
        private readonly outputChannel: vscode.LogOutputChannel,
        private readonly category: string | undefined = undefined,
        channels?: Map<string, vscode.LogOutputChannel>,
        createChannel?: (name: string) => vscode.LogOutputChannel,
    ) {
        super();
        this.createChannel = createChannel ?? ((name) => vscode.window.createOutputChannel(name, { log: true }));
        this.channels = channels ?? new Map([[outputChannel.name, outputChannel]]);
    }

    /**
     * Convenience factory: creates the root log output channel and a
     * matching logger in one call. The returned logger is the sole
     * owner of the channel — push it into `context.subscriptions`
     * directly instead of the channel.
     */
    static create(rootChannelName: string): OutputChannelLogger {
        const root = vscode.window.createOutputChannel(rootChannelName, { log: true });
        return new OutputChannelLogger(root);
    }

    override forCategory(category: LogCategory | string): ChannelLogger {
        return new OutputChannelLogger(this.outputChannel, category, this.channels, this.createChannel);
    }

    forChannel(name: string): ChannelLogger {
        let channel = this.channels.get(name);
        if (channel === undefined) {
            channel = this.createChannel(name);
            this.channels.set(name, channel);
        }
        return new OutputChannelLogger(channel, undefined, this.channels, this.createChannel);
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

    override log(level: LogLevel, message: string, error?: unknown): void {
        const line = this.category === undefined ? message : `[${this.category}] ${message}`;
        const channel = this.outputChannel;
        let log: (message: string, ...args: unknown[]) => void;

        switch (level) {
            case LogLevel.Trace: log = channel.trace; break;
            case LogLevel.Debug: log = channel.debug; break;
            case LogLevel.Info:  log = channel.info;  break;
            case LogLevel.Warn:  log = channel.warn;  break;
            case LogLevel.Error: log = channel.error; break;
            case LogLevel.Off:
                // `Off` is a configuration sentinel, never a call-site level. Reaching this branch
                // means a caller passed `LogLevel.Off` to one of the level methods (which they
                // can't — there's no `off()` method) or hand-rolled a `log(Off, …)` call.
                return;
            default: {
                const exhaustive: never = level;
                throw new Error(`Unhandled log level: ${exhaustive as number}`);
            }
        }

        // `.call(channel, …)` keeps `this` bound without allocating a wrapper, and lets us
        // branch the variadic arg in a single place instead of per-level.
        if (error === undefined) {
            log.call(channel, line);
        } else {
            log.call(channel, line, error);
        }
    }
}
