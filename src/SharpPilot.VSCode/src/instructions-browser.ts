import * as vscode from 'vscode';
import { instructions, type InstructionEntry } from './instructions-catalog.js';
import { instructionScheme } from './instructions-content-provider.js';

type BrowseItem = vscode.QuickPickItem & { entry?: InstructionEntry };

export class InstructionsBrowser {

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

        const uri = vscode.Uri.from({ scheme: instructionScheme, path: selected.entry.fileName });
        const doc = await vscode.workspace.openTextDocument(uri);
        await vscode.window.showTextDocument(doc, { preview: true, viewColumn: vscode.ViewColumn.Active });
    }
}
