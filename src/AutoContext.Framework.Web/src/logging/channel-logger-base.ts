import type { ChannelLogger } from './channel-logger.js';
import type { LogCategory } from './log-category.js';
import { LoggerBase } from './logger-base.js';

/**
 * Abstract base for {@link ChannelLogger} implementations. Extends
 * {@link LoggerBase} (so it inherits the level-method forwarders to
 * `log()`) and raises the surface to channel-aware loggers by
 * declaring `forChannel()`, `clear()`, and a covariant
 * `forCategory()` that returns {@link ChannelLogger}.
 *
 * `forChannel`/`clear` stay abstract — they are inherently
 * implementation-specific (vscode `LogOutputChannel`, file rotator,
 * in-memory buffer, etc.).
 */
export abstract class ChannelLoggerBase extends LoggerBase implements ChannelLogger {
    abstract override forCategory(category: LogCategory | string): ChannelLogger;
    abstract forChannel(name: string): ChannelLogger;
    abstract clear(): void;
}
