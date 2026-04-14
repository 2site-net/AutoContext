/**
 * Request to send a log message to the workspace service for centralized output.
 */
export interface LogRequest {
    readonly type: 'log';
    readonly source: string;
    readonly level: string;
    readonly message: string;
}
