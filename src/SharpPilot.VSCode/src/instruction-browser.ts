import * as vscode from 'vscode';
import { instructions, type InstructionEntry } from './config';

type BrowseItem = vscode.QuickPickItem & { entry?: InstructionEntry };

export class InstructionBrowser {
    constructor(private readonly extensionPath: string) {}

    async browse(): Promise<void> {
        const items: BrowseItem[] = [];
        let currentCategory = '';

        for (const entry of instructions) {
            if (entry.category !== currentCategory) {
                currentCategory = entry.category;
                items.push({ label: currentCategory, kind: vscode.QuickPickItemKind.Separator });
            }
            items.push({ label: entry.label, description: entry.category, entry });
        }

        const selected = await vscode.window.showQuickPick(items, {
            title: 'SharpPilot Instructions',
            placeHolder: 'Select an instruction to preview',
        });

        if (!selected?.entry) {
            return;
        }

        const uri = vscode.Uri.file(`${this.extensionPath}/instructions/${selected.entry.fileName}`);
        await vscode.commands.executeCommand('vscode.open', uri, { preview: true, viewColumn: vscode.ViewColumn.Active });
    }
}
