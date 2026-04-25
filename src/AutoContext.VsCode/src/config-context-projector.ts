import * as vscode from 'vscode';
import type { AutoContextConfigManager } from './autocontext-config.js';
import type { InstructionsCatalog } from './instructions-catalog.js';
import type { McpToolsManifest } from './mcp-tools-manifest.js';
import type { AutoContextConfig } from './types/autocontext-config.js';

export function isToolEnabled(config: AutoContextConfig, toolName: string, taskName?: string): boolean {
    const entry = config.mcpTools?.[toolName];
    if (entry === undefined) return true;
    if (taskName) {
        // Tasks are independent of the parent's enabled state.
        // `entry === false` (shorthand) has no disabledTasks → task is enabled.
        if (entry === false) return true;
        return !entry.disabledTasks?.includes(taskName);
    }
    if (entry === false) return false;
    if (entry.enabled === false) return false;
    return true;
}

export class ConfigContextProjector implements vscode.Disposable {
    private readonly disposables: vscode.Disposable[] = [];

    constructor(
        private readonly configManager: AutoContextConfigManager,
        private readonly instructionsCatalog: InstructionsCatalog,
        private readonly toolsManifest: McpToolsManifest,
        private readonly outputChannel: vscode.OutputChannel,
    ) {
        this.disposables.push(
            configManager.onDidChange(() =>
                void this.project().catch(err =>
                    this.outputChannel.appendLine(`[ConfigProjector] Failed to project config: ${err instanceof Error ? err.message : err}`),
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
                toolKeys.push(setContext(tool.runtimeInfo.contextKey, isToolEnabled(config, tool.name)));
            } else {
                for (const task of tool.tasks) {
                    toolKeys.push(setContext(task.runtimeInfo.contextKey, isToolEnabled(config, tool.name, task.name)));
                }
            }
        }

        await Promise.all([
            ...this.instructionsCatalog.all.map(entry =>
                setContext(entry.contextKey, config.instructions?.[entry.fileName]?.enabled !== false),
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
