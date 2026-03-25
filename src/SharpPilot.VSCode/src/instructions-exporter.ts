import * as vscode from 'vscode';
import { instructions, targetPath, type InstructionEntry } from './config.js';
import { hashContent, manifestRelativePath, readManifest, writeManifest } from './export-manifest.js';

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
        const manifestUri = vscode.Uri.joinPath(rootUri, manifestRelativePath);
        const manifest = await readManifest(manifestUri) ?? { exports: {} };
        const exported: string[] = [];

        for (const { entry } of selected) {
            const target = targetPath(entry);
            const targetUri = vscode.Uri.joinPath(rootUri, target);

            const exists = await fileExists(targetUri);

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

            const hash = await copyInstruction(this.extensionPath, entry, targetUri);
            manifest.exports[entry.fileName] = { hash };
            exported.push(entry.label);
        }

        if (exported.length > 0) {
            await writeManifest(manifestUri, manifest);

            const lastUri = vscode.Uri.joinPath(rootUri, targetPath(selected[selected.length - 1].entry));

            await vscode.window.showTextDocument(lastUri);
            await vscode.window.showInformationMessage(`Exported ${exported.length} instruction(s) to .github.`);
        }
    }
}

async function copyInstruction(extensionPath: string, entry: InstructionEntry, targetUri: vscode.Uri): Promise<string> {
    const sourceUri = vscode.Uri.file(`${extensionPath}/instructions/.generated/${entry.fileName}`);
    const content = await vscode.workspace.fs.readFile(sourceUri);

    await vscode.workspace.fs.writeFile(targetUri, content);

    return hashContent(content);
}

async function fileExists(uri: vscode.Uri): Promise<boolean> {
    try {
        await vscode.workspace.fs.stat(uri);
        return true;
    } catch {
        return false;
    }
}
