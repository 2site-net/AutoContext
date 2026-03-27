import * as vscode from 'vscode';
import { instructionsCatalog } from './instructions-catalog.js';
import { tools } from './tool-entry.js';

export class StatusBarIndicator implements vscode.Disposable {
    static readonly commandId = 'sharppilot.showToggleMenu';

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
        const enabledInstructions = instructionsCatalog.all.filter(i => config.get<boolean>(i.settingId, true)).length;
        const enabledTools = tools.filter(t => config.get<boolean>(t.settingId, true)).length;
        this.item.text = `$(book) ${enabledInstructions}/${instructionsCatalog.count} $(tools) ${enabledTools}/${tools.length}`;
        this.item.tooltip = `Instructions: ${enabledInstructions}/${instructionsCatalog.count} enabled, Tools: ${enabledTools}/${tools.length} enabled — click to toggle`;
    }

    async showToggleMenu(): Promise<void> {
        const picked = await vscode.window.showQuickPick(
            [
                { label: '$(book) Toggle Instructions', command: 'sharppilot.toggleInstructions' },
                { label: '$(tools) Toggle Tools', command: 'sharppilot.toggleTools' },
                { label: '$(sparkle) Auto Configure', command: 'sharppilot.autoConfigure' },
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
