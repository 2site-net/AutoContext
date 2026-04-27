import * as vscode from 'vscode';
import type { InstructionsFileEntry } from './instructions-file-entry.js';
import { instructionScheme } from './instructions-viewer-document-provider.js';
import type { Logger } from '#types/logger.js';

export class InstructionsFilesExporter {
    constructor(
        private readonly extensionPath: string,
        private readonly logger: Logger,
    ) {}

    async export(entries: readonly InstructionsFileEntry[]): Promise<void> {
        const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
        if (!workspaceFolder || entries.length === 0) { return; }

        const rootUri = workspaceFolder.uri;
        let exportedCount = 0;

        for (const entry of entries) {
            const targetUri = vscode.Uri.joinPath(rootUri, entry.targetPath);
            const exists = await this.fileExists(targetUri);

            if (exists) {
                const action = await vscode.window.showWarningMessage(
                    `'${entry.targetPath}' already exists.`,
                    'Overwrite',
                    'Skip',
                );

                if (action !== 'Overwrite') { continue; }
            }

            await InstructionsFilesExporter.copyInstruction(this.extensionPath, entry, targetUri);
            await InstructionsFilesExporter.closeVirtualDocument(entry.name);
            exportedCount++;
        }

        if (exportedCount > 0) {
            await vscode.window.showInformationMessage(`Exported ${exportedCount} instruction(s) to .github.`);
        }
    }

    private static async copyInstruction(extensionPath: string, entry: InstructionsFileEntry, targetUri: vscode.Uri): Promise<void> {
        const sourceUri = vscode.Uri.file(`${extensionPath}/instructions/.generated/${entry.name}`);
        const content = await vscode.workspace.fs.readFile(sourceUri);

        await vscode.workspace.fs.writeFile(targetUri, content);
    }

    private async fileExists(uri: vscode.Uri): Promise<boolean> {
        try {
            await vscode.workspace.fs.stat(uri);
            return true;
        } catch (err) {
            this.logger.debug(`Existence check failed (treating as missing): ${uri.fsPath}`, err);
            return false;
        }
    }

    private static async closeVirtualDocument(fileName: string): Promise<void> {
        const virtualUri = vscode.Uri.from({ scheme: instructionScheme, path: fileName });
        for (const tab of vscode.window.tabGroups.all.flatMap(g => g.tabs)) {
            const tabUri = (tab.input as { uri?: vscode.Uri })?.uri;
            if (tabUri?.toString() === virtualUri.toString()) {
                await vscode.window.tabGroups.close(tab);
            }
        }
    }
}
