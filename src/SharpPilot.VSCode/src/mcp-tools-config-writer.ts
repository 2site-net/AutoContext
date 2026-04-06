import * as vscode from 'vscode';
import { McpToolsRegistry } from './mcp-tools-registry.js';
import type { SharpPilotConfigManager } from './sharppilot-config.js';

export class McpToolsConfigWriter {
    constructor(private readonly configManager: SharpPilotConfigManager) {}

    write(): void {
        const config = vscode.workspace.getConfiguration();
        const disabledTools: string[] = [];

        for (const tool of McpToolsRegistry.all) {
            if (config.get<boolean>(tool.settingId, true) === false) {
                disabledTools.push(tool.featureName ?? tool.toolName);
            }
        }

        this.configManager.setDisabledTools(disabledTools);
    }
}
