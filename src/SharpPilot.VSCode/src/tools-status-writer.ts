import * as vscode from 'vscode';
import { toolsCatalog } from './tools-catalog.js';
import type { SharpPilotConfigManager } from './sharppilot-config.js';

export class ToolsStatusWriter {
    constructor(private readonly configManager: SharpPilotConfigManager) {}

    write(): void {
        const config = vscode.workspace.getConfiguration();
        const disabledTools: string[] = [];

        for (const tool of toolsCatalog.all) {
            if (config.get<boolean>(tool.settingId, true) === false) {
                disabledTools.push(tool.toolName);
            }
        }

        this.configManager.setDisabledTools(disabledTools);
    }
}
