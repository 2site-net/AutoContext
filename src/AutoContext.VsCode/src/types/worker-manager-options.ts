import type { ServerEntry } from '../server-entry.js';
import type { Logger } from './logger.js';

/**
 * Construction options for `WorkerManager`.
 */
export interface WorkerManagerOptions {
    readonly extensionPath: string;
    readonly logger: Logger;
    readonly workspaceRoot: string | undefined;
    readonly workers: readonly ServerEntry[];
    readonly instanceId: string;
    readonly logServiceAddress?: string;
    readonly healthMonitorServiceAddress?: string;
}
