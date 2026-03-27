import * as vscode from 'vscode';
import type { InstructionsCatalogEntry } from './instructions-catalog-entry.js';
import { getUnexportedInstructions } from './instructions-export-state.js';
import { instructionScheme } from './instructions-content-provider.js';

type BrowseItem = vscode.QuickPickItem & { entry?: InstructionsCatalogEntry };

export class InstructionsBrowser {

    async browse(): Promise<void> {
        const availableInstructions = await getUnexportedInstructions();
        if (availableInstructions.length === 0) {
            await vscode.window.showInformationMessage('All instructions are exported. Delete one to browse it here again.');
            return;
        }

        const items: BrowseItem[] = [];
        let currentCategory = '';

        for (const entry of availableInstructions) {
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
