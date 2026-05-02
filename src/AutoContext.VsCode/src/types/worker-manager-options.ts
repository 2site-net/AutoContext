import type { ServerEntry } from '../server-entry.js';
import type { ChannelLogger } from 'autocontext-framework-web';

/**
 * Construction options for `WorkerManager`.
 */
export interface WorkerManagerOptions {
    readonly extensionPath: string;
    readonly logger: ChannelLogger;
    readonly workspaceRoot: string | undefined;
    readonly workers: readonly ServerEntry[];
    readonly instanceId: string;
    readonly logServiceAddress?: string;
    readonly healthMonitorServiceAddress?: string;
}
