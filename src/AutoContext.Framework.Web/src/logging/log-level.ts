/**
 * Severity levels for {@link Logger} and {@link LoggerFacade}.
 * Numeric ordering matches `Microsoft.Extensions.Logging.LogLevel` on
 * the .NET side so the two sides can eventually share a single
 * configuration knob.
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
