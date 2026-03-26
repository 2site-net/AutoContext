import * as vscode from 'vscode';
import { instructions, targetPath, type InstructionEntry } from './instructions-catalog.js';

export class InstructionsExporter {
    constructor(private readonly extensionPath: string) {}

    async export(): Promise<void> {
        const items = instructions.map(entry => ({
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

        const workspaceFolder = vscode.workspace.workspaceFolders?.[0];

        if (!workspaceFolder) {
            await vscode.window.showErrorMessage('No workspace folder open.');
            return;
        }

        const rootUri = workspaceFolder.uri;
        const exported: string[] = [];

        for (const { entry } of selected) {
            const target = targetPath(entry);
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
            const lastUri = vscode.Uri.joinPath(rootUri, targetPath(selected[selected.length - 1].entry));

            await vscode.window.showTextDocument(lastUri);
            await vscode.window.showInformationMessage(`Exported ${exported.length} instruction(s) to .github.`);
        }
    }

    private static async copyInstruction(extensionPath: string, entry: InstructionEntry, targetUri: vscode.Uri): Promise<void> {
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
