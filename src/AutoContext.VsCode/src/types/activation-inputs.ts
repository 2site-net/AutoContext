import type * as vscode from 'vscode';
import type { ChannelLogger } from 'autocontext-framework-web';
import type { ExtensionGraph } from '../extension-composition.js';

/**
 * Inputs needed to drive the async portion of extension activation
 * via `ExtensionActivator.run()`.
 */
export interface ActivationInputs {
    readonly context: vscode.ExtensionContext;
    readonly graph: ExtensionGraph;
    readonly didChangeEmitter: vscode.EventEmitter<void>;
    readonly version: string;
    readonly rootLogger: ChannelLogger;
}
