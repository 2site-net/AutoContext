/**
 * Log severity levels. Names match the .NET `LogLevel` enum so the
 * extension-side LogServer can route both .NET and Node worker output
 * through the same `levelToMethod` mapper.
 */
export type LogLevel = 'Trace' | 'Debug' | 'Information' | 'Warning' | 'Error' | 'Critical';

/**
 * In-process record enqueued by every {@link PipeLogger} call. Drained
 * off-thread by `LoggingClient` and either sent over the pipe (the
 * extension is listening) or written to stderr (fallback).
 *
 * TypeScript counterpart of the C# `LogEntry` readonly struct in
 * `AutoContext.Worker.Logging`.
 */
export interface LogEntry {
    readonly category: string;
    readonly level: LogLevel;
    readonly message: string;
    readonly exception?: string;
    readonly correlationId?: string;
}

/**
 * Wire shape for one NDJSON log record streamed from a worker to the
 * extension's LogServer. Property names are intentionally lowercased
 * to keep the serialized payload compact and to match the
 * `AutoContext.Worker.Shared` C# `JsonLogEntry`.
 */
export interface JsonLogEntry {
    readonly category: string;
    readonly level: string;
    readonly message: string;
    readonly exception?: string;
    readonly correlationId?: string;
}

/**
 * Wire shape for the greeting line every `LoggingClient` sends as
 * the very first NDJSON line on the pipe — lets the extension route
 * subsequent records to the per-worker output channel.
 */
export interface JsonLogGreeting {
    readonly clientName: string;
}
