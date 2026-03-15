import * as vscode from 'vscode';
import { instructions, tools } from './config';

export class StatusBarIndicator implements vscode.Disposable {
    private readonly item: vscode.StatusBarItem;

    constructor() {
        this.item = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
        this.item.name = 'QA-MCP Instructions';
        this.item.command = 'qa-mcp.toggleInstructions';
        this.update();
        this.item.show();
    }

    update(): void {
        const config = vscode.workspace.getConfiguration();
        const enabledInstructions = instructions.filter(i => config.get<boolean>(i.settingId, true)).length;
        const enabledTools = tools.filter(t => config.get<boolean>(t.settingId, true)).length;
        const enabled = enabledInstructions + enabledTools;
        const total = instructions.length + tools.length;
        this.item.text = `$(checklist) QA-MCP: ${enabled}/${total}`;
        this.item.tooltip = `${enabled} of ${total} items enabled — click to toggle`;
    }

    dispose(): void {
        this.item.dispose();
    }
}
