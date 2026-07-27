// Wire shapes of the Logs.* surface.

/** Structured event id attached to a log record. */
export interface JsonLogEventId {
    readonly id: number;
    readonly name?: string;
}

/** Exception detail attached to a log record. */
export interface JsonLogExceptionInfo {
    readonly type: string;
    readonly message: string;
    readonly stackTrace: string;
    readonly inner?: JsonLogExceptionInfo;
}

/** Canonical log-record envelope, shared by disk and wire. */
export interface JsonLogRecord {
    readonly timestamp: string;
    readonly category: string;
    readonly level: string;
    readonly eventId?: JsonLogEventId;
    readonly message: string;
    readonly properties?: Readonly<Record<string, unknown>>;
    readonly exception?: JsonLogExceptionInfo;
}

/** Parameters of Logs.GetEngine. */
export interface JsonLogsGetEngineParams {
    readonly lastN?: number;
    readonly since?: string;
}

/** Result of Logs.GetEngine. */
export interface JsonLogsGetEngineResult {
    readonly records: readonly JsonLogRecord[];
    readonly truncated: boolean;
}

/** Parameters of Logs.GetWorker. */
export interface JsonLogsGetWorkerParams {
    readonly workerId?: string;
    readonly lastN?: number;
    readonly since?: string;
}

/** Result of Logs.GetWorker. */
export type JsonLogsGetWorkerResult =
    | {
        readonly kind: 'ok';
        readonly records: readonly JsonLogRecord[];
        readonly truncated: boolean;
    }
    | { readonly kind: 'not-found'; readonly workerId?: string };

/** Parameters of Logs.TailWorker. */
export interface JsonLogsTailWorkerParams {
    readonly workerId?: string;
}

/** One frame of a Logs.Tail* stream. */
export type JsonLogStreamFrame =
    | { readonly kind: 'record'; readonly record: JsonLogRecord }
    | { readonly kind: 'dropped'; readonly reason: string }
    | { readonly kind: 'not-found'; readonly workerId: string };
