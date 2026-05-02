import type { LogCategory } from './log-category.js';
import type { LoggerFacade } from './logger-facade.js';
import type { Logger } from './logger.js';

/**
 * Channel-aware logger surface: combines {@link Logger} and
 * {@link LoggerFacade} (so callers can both write records via the
 * narrow sink and via the level-method conveniences) and adds the
 * ability to derive child loggers by category, derive sibling loggers
 * backed by a different output channel, and clear the current
 * channel.
 *
 * Implementations are typically backed by a host-provided sink such
 * as a VS Code `LogOutputChannel`, a file rotator, or an in-memory
 * buffer. The host owns disposal of the underlying channels.
 */
export interface ChannelLogger extends Logger, LoggerFacade {
    /**
     * Returns a child logger that prepends the given category to
     * every line. Calling {@link forCategory} on a child replaces
     * (does not append to) the existing category. Returns
     * {@link ChannelLogger} so chained children retain the channel
     * surface.
     */
    forCategory(category: LogCategory | string): ChannelLogger;

    /**
     * Returns a logger bound to a sibling output channel with the
     * given name. The channel is created on first request and cached
     * at the root logger so repeated calls — from anywhere in the
     * parent/child tree — return loggers backed by the same channel.
     * The returned logger has no category by default; chain
     * `.forCategory(...)` if needed.
     */
    forChannel(name: string): ChannelLogger;

    /**
     * Clears the output channel this logger writes to. The channel
     * may be shared with sibling loggers — anything else derived from
     * the same {@link forChannel} name or chained off this logger via
     * {@link forCategory} writes to the same channel and will lose
     * its history too. Reserve `clear()` for loggers bound to a
     * dedicated channel obtained via {@link forChannel}.
     */
    clear(): void;
}
