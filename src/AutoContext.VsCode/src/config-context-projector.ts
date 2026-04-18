import * as vscode from 'vscode';
import type { AutoContextConfigManager } from './autocontext-config.js';
import type { InstructionsCatalog } from './instructions-catalog.js';
import type { McpToolsCatalog } from './mcp-tools-catalog.js';
import type { AutoContextConfig } from './types/autocontext-config.js';

export function isToolEnabled(config: AutoContextConfig, toolName: string, featureName?: string): boolean {
    const entry = config.mcpTools?.[toolName];
    if (entry === undefined) return true;
    if (entry === false) return false;
    if (entry.enabled === false) return false;
    if (featureName && entry.disabledFeatures?.includes(featureName)) return false;
    return true;
}

export class ConfigContextProjector implements vscode.Disposable {
    private readonly disposables: vscode.Disposable[] = [];

    constructor(
        private readonly configManager: AutoContextConfigManager,
        private readonly instructionsCatalog: InstructionsCatalog,
        private readonly toolsCatalog: McpToolsCatalog,
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

        await Promise.all([
            ...this.instructionsCatalog.all.map(entry =>
                setContext(entry.contextKey, config.instructions?.[entry.fileName]?.enabled !== false),
            ),
            ...this.toolsCatalog.all.map(entry =>
                setContext(entry.contextKey, isToolEnabled(config, entry.toolName, entry.featureName)),
            ),
        ]);
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }
}
