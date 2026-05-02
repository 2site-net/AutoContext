import type * as vscode from 'vscode';
import type { ChannelLogger } from 'autocontext-framework-web';
import type { ExtensionGraph } from '../extension-composition.js';

/**
 * Inputs needed to register the extension's VS Code surfaces via
 * `ExtensionRegistrar.register()`.
 */
export interface RegistrationInputs {
    readonly context: vscode.ExtensionContext;
    readonly graph: ExtensionGraph;
    readonly didChangeEmitter: vscode.EventEmitter<void>;
    readonly rootLogger: ChannelLogger;
}
