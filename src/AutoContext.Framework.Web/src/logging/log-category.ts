/**
 * Canonical category names for `forCategory()` derivation on
 * {@link LoggerBase} / {@link ChannelLogger}. Hand-typed `[Bracket]`
 * prefixes drift over time (we already had `[Instructions]` and
 * `[InstructionsWriter]` pointing at the same subsystem); using
 * these constants keeps the set grep-able and prevents accidental
 * new labels.
 *
 * Subsystems that produce dynamic categories (e.g. per-worker
 * process names) may also pass a freeform string to `forCategory()`.
 */
export const LogCategory = {
    Activation: 'Activation',
    Config: 'Config',
    ConfigProjector: 'ConfigProjector',
    ConfigServer: 'ConfigServer',
    Decorations: 'Decorations',
    Detection: 'Detection',
    Diagnostics: 'Diagnostics',
    HealthMonitor: 'HealthMonitor',
    Instructions: 'Instructions',
    InstructionsTree: 'InstructionsTree',
    InstructionsWriter: 'InstructionsWriter',
    LogServer: 'LogServer',
    McpServerProvider: 'McpServerProvider',
    McpToolsTree: 'McpToolsTree',
    WorkerControl: 'WorkerControl',
    WorkerManager: 'WorkerManager',
} as const;
export type LogCategory = typeof LogCategory[keyof typeof LogCategory];
