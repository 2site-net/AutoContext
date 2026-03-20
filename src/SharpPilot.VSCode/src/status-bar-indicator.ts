import * as vscode from 'vscode';
import { instructions, tools } from './config';

export class StatusBarIndicator implements vscode.Disposable {
    static readonly commandId = 'sharp-pilot.showToggleMenu';

    private readonly item: vscode.StatusBarItem;

    constructor() {
        this.item = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
        this.item.name = 'SharpPilot';
        this.item.command = StatusBarIndicator.commandId;
        this.update();
        this.item.show();
    }

    update(): void {
        const config = vscode.workspace.getConfiguration();
        const enabledInstructions = instructions.filter(i => config.get<boolean>(i.settingId, true)).length;
        const enabledTools = tools.filter(t => config.get<boolean>(t.settingId, true)).length;
        this.item.text = `$(book) ${enabledInstructions}/${instructions.length} $(tools) ${enabledTools}/${tools.length}`;
        this.item.tooltip = `Instructions: ${enabledInstructions}/${instructions.length} enabled, Tools: ${enabledTools}/${tools.length} enabled — click to toggle`;
    }

    async showToggleMenu(): Promise<void> {
        const picked = await vscode.window.showQuickPick(
            [
                { label: '$(book) Toggle Instructions', command: 'sharp-pilot.toggleInstructions' },
                { label: '$(tools) Toggle Tools', command: 'sharp-pilot.toggleTools' },
                { label: '$(sparkle) Auto Configure', command: 'sharp-pilot.autoConfigure' },
            ],
            { title: 'SharpPilot', placeHolder: 'What would you like to toggle?' },
        );

        if (picked) {
            await vscode.commands.executeCommand(picked.command);
        }
    }

    dispose(): void {
        this.item.dispose();
    }
}
