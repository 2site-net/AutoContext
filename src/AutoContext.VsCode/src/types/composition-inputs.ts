import type * as vscode from 'vscode';
import type { ChannelLogger } from 'autocontext-framework-web';

/**
 * Inputs needed to construct the extension's full object graph via
 * `ExtensionComposer.compose()`.
 *
 * `compose()` is intentionally synchronous and side-effect-free
 * beyond `new` calls — anything that needs to await goes into
 * `ExtensionActivator.run()` instead.
 */
export interface CompositionInputs {
    readonly extensionPath: string;
    readonly version: string;
    readonly workspaceRoot: string | undefined;
    readonly instanceId: string;
    readonly didChangeEmitter: vscode.EventEmitter<void>;
    readonly rootLogger: ChannelLogger & vscode.Disposable;
}
