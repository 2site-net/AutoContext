/**
 * Severity levels for {@link Logger}.  Numeric ordering matches
 * Microsoft.Extensions.Logging on the .NET side so the two sides can
 * eventually share a single configuration knob.
 */
export const LogLevel = {
    Trace: 0,
    Debug: 1,
    Info: 2,
    Warn: 3,
    Error: 4,
    Off: 5,
} as const;
export type LogLevel = typeof LogLevel[keyof typeof LogLevel];

/**
 * Canonical category names. Hand-typed `[Bracket]` prefixes drift over
 * time (we already had `[Instructions]` and `[InstructionsWriter]`
 * pointing at the same subsystem); using these constants keeps the set
 * grep-able and prevents accidental new labels.
 *
 * Subsystems that produce dynamic categories (e.g. per-worker process
 * names from `worker-manager.ts`) may also pass a freeform string to
 * {@link Logger.forCategory}.
 */
export const LogCategory = {
    Activation: 'Activation',
    Config: 'Config',
    ConfigProjector: 'ConfigProjector',
    Decorations: 'Decorations',
    Detection: 'Detection',
    Diagnostics: 'Diagnostics',
    HealthMonitor: 'HealthMonitor',
    Instructions: 'Instructions',
    InstructionsTree: 'InstructionsTree',
    InstructionsWriter: 'InstructionsWriter',
    McpServerProvider: 'McpServerProvider',
    McpToolsTree: 'McpToolsTree',
    WorkerManager: 'WorkerManager',
} as const;
export type LogCategory = typeof LogCategory[keyof typeof LogCategory];

/**
 * Minimal logging facade for the extension. Replaces direct
 * `OutputChannel.appendLine` calls so that:
 *   - severity is recorded (and can be filtered by the user),
 *   - error formatting is centralized (no more duplicated
 *     `err instanceof Error ? err.message : err` ternaries),
 *   - category names come from a known set instead of free-form
 *     bracket prefixes.
 *
 * Implementations should be cheap to construct via {@link forCategory}
 * — call sites typically take the root `Logger`, then derive a
 * per-class child once in their constructor.
 */
export interface Logger {
    trace(message: string, error?: unknown): void;
    debug(message: string, error?: unknown): void;
    info(message: string, error?: unknown): void;
    warn(message: string, error?: unknown): void;
    error(message: string, error?: unknown): void;

    /**
     * Returns a child logger that prepends the given category to every
     * line. Calling {@link forCategory} on a child replaces (does not
     * append to) the existing category.
     */
    forCategory(category: LogCategory | string): Logger;

    /**
     * Returns a logger bound to a sibling output channel with the given
     * name. The channel is created on first request and cached at the
     * root logger so repeated calls — from anywhere in the parent/child
     * tree — return loggers backed by the same channel. The returned
     * logger has no category by default; chain `.forCategory(...)` if
     * needed. The root logger owns disposal of every channel created
     * this way.
     */
    forChannel(name: string): Logger;

    /**
     * Clears the output channel this logger writes to. The channel may
     * be shared with sibling loggers — anything else derived from the
     * same {@link forChannel} name or chained off this logger via
     * {@link forCategory} writes to the same channel and will lose its
     * history too. Reserve `clear()` for loggers bound to a dedicated
     * channel obtained via {@link forChannel}.
     */
    clear(): void;
}
