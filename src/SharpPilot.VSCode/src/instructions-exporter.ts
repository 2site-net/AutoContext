import * as vscode from 'vscode';
import type { InstructionsCatalogEntry } from './instructions-catalog-entry.js';
import { InstructionsExportState } from './instructions-export-state.js';

export class InstructionsExporter {
    constructor(private readonly extensionPath: string) {}

    async export(): Promise<void> {
        const workspaceFolder = vscode.workspace.workspaceFolders?.[0];

        if (!workspaceFolder) {
            await vscode.window.showErrorMessage('No workspace folder open.');
            return;
        }

        const rootUri = workspaceFolder.uri;

        const availableInstructions = await InstructionsExportState.getUnexportedFiles();
        if (availableInstructions.length === 0) {
            await vscode.window.showInformationMessage('All instructions are already exported.');
            return;
        }

        const items = availableInstructions.map(entry => ({
            label: entry.label,
            description: entry.category,
            entry,
        }));

        const selected = await vscode.window.showQuickPick(items, {
            canPickMany: true,
            title: 'SharpPilot: Export Instructions',
            placeHolder: 'Select instructions to export to .github',
        });

        if (!selected || selected.length === 0) {
            return;
        }
        const exported: string[] = [];

        for (const { entry } of selected) {
            const target = entry.targetPath;
            const targetUri = vscode.Uri.joinPath(rootUri, target);

            const exists = await InstructionsExporter.fileExists(targetUri);

            if (exists) {
                const action = await vscode.window.showWarningMessage(
                    `'${target}' already exists.`,
                    'Overwrite',
                    'Open Existing',
                    'Skip',
                );

                if (action === 'Open Existing') {
                    await vscode.window.showTextDocument(targetUri);
                    continue;
                }

                if (action !== 'Overwrite') {
                    continue;
                }
            }

            await InstructionsExporter.copyInstruction(this.extensionPath, entry, targetUri);
            exported.push(entry.label);
        }

        if (exported.length > 0) {
            const lastUri = vscode.Uri.joinPath(rootUri, selected[selected.length - 1].entry.targetPath);

            await vscode.window.showTextDocument(lastUri);
            await vscode.window.showInformationMessage(`Exported ${exported.length} instruction(s) to .github.`);
        }
    }

    async exportEntries(entries: readonly InstructionsCatalogEntry[]): Promise<void> {
        const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
        if (!workspaceFolder || entries.length === 0) { return; }

        const rootUri = workspaceFolder.uri;
        let exportedCount = 0;

        for (const entry of entries) {
            const targetUri = vscode.Uri.joinPath(rootUri, entry.targetPath);
            const exists = await InstructionsExporter.fileExists(targetUri);

            if (exists) {
                const action = await vscode.window.showWarningMessage(
                    `'${entry.targetPath}' already exists.`,
                    'Overwrite',
                    'Skip',
                );

                if (action !== 'Overwrite') { continue; }
            }

            await InstructionsExporter.copyInstruction(this.extensionPath, entry, targetUri);
            exportedCount++;
        }

        if (exportedCount > 0) {
            await vscode.window.showInformationMessage(`Exported ${exportedCount} instruction(s) to .github.`);
        }
    }

    private static async copyInstruction(extensionPath: string, entry: InstructionsCatalogEntry, targetUri: vscode.Uri): Promise<void> {
        const sourceUri = vscode.Uri.file(`${extensionPath}/instructions/.generated/${entry.fileName}`);
        const content = await vscode.workspace.fs.readFile(sourceUri);

        await vscode.workspace.fs.writeFile(targetUri, content);
    }

    private static async fileExists(uri: vscode.Uri): Promise<boolean> {
        try {
            await vscode.workspace.fs.stat(uri);
            return true;
        } catch {
            return false;
        }
    }
}
