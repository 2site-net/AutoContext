import * as vscode from 'vscode';
import type { McpToolsCatalog } from './mcp-tools-catalog.js';
import type { SharpPilotConfigManager } from './sharppilot-config.js';

export class McpToolsConfigWriter {
    constructor(private readonly configManager: SharpPilotConfigManager, private readonly catalog: McpToolsCatalog) {}

    write(): void {
        const config = vscode.workspace.getConfiguration();
        const disabledTools: string[] = [];

        for (const tool of this.catalog.all) {
            if (config.get<boolean>(tool.settingId, true) === false) {
                disabledTools.push(tool.featureName ?? tool.toolName);
            }
        }

        this.configManager.setDisabledTools(disabledTools);
    }
}
