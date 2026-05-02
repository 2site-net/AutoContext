import * as vscode from 'vscode';
import type { AutoContextConfigManager } from './autocontext-config-manager.js';
import type { InstructionsFilesManifest } from './instructions-files-manifest.js';
import type { McpToolsManifest } from './mcp-tools-manifest.js';
import type { ChannelLogger } from 'autocontext-framework-web';

export class AutoContextProjector implements vscode.Disposable {
    private readonly disposables: vscode.Disposable[] = [];

    constructor(
        private readonly configManager: AutoContextConfigManager,
        private readonly instructionsManifest: InstructionsFilesManifest,
        private readonly toolsManifest: McpToolsManifest,
        private readonly logger: ChannelLogger,
    ) {
        this.disposables.push(
            configManager.onDidChange(() =>
                void this.project().catch(err =>
                    this.logger.error('Failed to project config', err),
                ),
            ),
        );
    }

    async project(): Promise<void> {
        const config = await this.configManager.read();
        const setContext = (key: string, value: boolean): Thenable<void> =>
            vscode.commands.executeCommand('setContext', key, value);

        const toolKeys: Thenable<void>[] = [];
        for (const tool of this.toolsManifest.tools) {
            if (tool.tasks.length === 0) {
                toolKeys.push(setContext(tool.runtimeInfo.contextKey, config.isToolEnabled(tool.name)));
            } else {
                for (const task of tool.tasks) {
                    toolKeys.push(setContext(task.runtimeInfo.contextKey, config.isToolEnabled(tool.name, task.name)));
                }
            }
        }

        await Promise.all([
            ...this.instructionsManifest.instructions.map(entry =>
                setContext(entry.runtimeInfo.contextKey, config.instructions?.[entry.name]?.enabled !== false),
            ),
            ...toolKeys,
        ]);
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }
}
