import * as vscode from 'vscode';
import type { AutoContextConfigManager } from './autocontext-config.js';
import type { InstructionsCatalog } from './instructions-catalog.js';
import type { McpToolsCatalog } from './mcp-tools-catalog.js';
import type { AutoContextConfig } from './types/autocontext-config.js';

export class ConfigContextProjector implements vscode.Disposable {
    private readonly disposables: vscode.Disposable[] = [];

    constructor(
        private readonly configManager: AutoContextConfigManager,
        private readonly instructionsCatalog: InstructionsCatalog,
        private readonly toolsCatalog: McpToolsCatalog,
    ) {
        this.disposables.push(
            configManager.onDidChange(() => void this.project()),
        );
    }

    async project(): Promise<void> {
        const config = await this.configManager.read();
        const setContext = (key: string, value: boolean): Thenable<void> =>
            vscode.commands.executeCommand('setContext', key, value);

        await Promise.all([
            ...this.instructionsCatalog.all.map(entry =>
                setContext(entry.settingId, config.instructions?.[entry.fileName]?.enabled !== false),
            ),
            ...this.toolsCatalog.all.map(entry =>
                setContext(entry.settingId, ConfigContextProjector.isToolEnabled(config, entry.toolName, entry.featureName)),
            ),
        ]);
    }

    static isToolEnabled(config: AutoContextConfig, toolName: string, featureName?: string): boolean {
        const entry = config.mcpTools?.[toolName];
        if (entry === undefined) return true;
        if (entry === false) return false;
        if (entry.enabled === false) return false;
        if (featureName && entry.disabledFeatures?.includes(featureName)) return false;
        return true;
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }
}
