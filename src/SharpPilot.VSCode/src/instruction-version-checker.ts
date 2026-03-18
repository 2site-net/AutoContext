import * as vscode from 'vscode';
import { instructions, targetPath, type InstructionEntry } from './config';
import { manifestRelativePath, readManifest, writeManifest } from './export-manifest';

export class InstructionVersionChecker {
    constructor(private readonly extensionPath: string) {}

    async check(): Promise<void> {
        const workspaceFolder = vscode.workspace.workspaceFolders?.[0];

        if (!workspaceFolder) {
            return;
        }

        const manifestUri = vscode.Uri.joinPath(workspaceFolder.uri, manifestRelativePath);
        const manifest = await readManifest(manifestUri);

        if (!manifest) {
            return;
        }

        const outdated = instructions.filter(
            i => manifest.exports[i.fileName] && manifest.exports[i.fileName].version < i.version,
        );

        if (outdated.length === 0) {
            return;
        }

        const names = outdated.map(i => i.label).join(', ');
        const action = await vscode.window.showWarningMessage(
            `SharpPilot: ${outdated.length} exported instruction(s) are outdated: ${names}`,
            'View Details',
            'Update All',
            'Dismiss',
        );

        if (action === 'View Details') {
            await this.viewDetails(workspaceFolder.uri, outdated);
        } else if (action === 'Update All') {
            await this.updateAll(workspaceFolder.uri, manifestUri, manifest, outdated);
        }
    }

    private async viewDetails(rootUri: vscode.Uri, outdated: readonly InstructionEntry[]): Promise<void> {
        const entry = outdated.length === 1
            ? outdated[0]
            : (await vscode.window.showQuickPick(
                outdated.map(i => ({ label: i.label, description: i.category, entry: i })),
                { title: 'Select instruction to compare' },
            ))?.entry;

        if (!entry) {
            return;
        }

        const sourceUri = vscode.Uri.file(`${this.extensionPath}/instructions/${entry.fileName}`);
        const exportedUri = vscode.Uri.joinPath(rootUri, targetPath(entry));

        await vscode.commands.executeCommand('vscode.diff', exportedUri, sourceUri, `${entry.label}: Exported ↔ Current`);
    }

    private async updateAll(
        rootUri: vscode.Uri,
        manifestUri: vscode.Uri,
        manifest: { exports: Record<string, { version: number }> },
        outdated: readonly InstructionEntry[],
    ): Promise<void> {
        for (const entry of outdated) {
            const sourceUri = vscode.Uri.file(`${this.extensionPath}/instructions/${entry.fileName}`);
            const exportedUri = vscode.Uri.joinPath(rootUri, targetPath(entry));
            const content = await vscode.workspace.fs.readFile(sourceUri);

            await vscode.workspace.fs.writeFile(exportedUri, content);
            manifest.exports[entry.fileName] = { version: entry.version };
        }

        await writeManifest(manifestUri, manifest);
        await vscode.window.showInformationMessage(`Updated ${outdated.length} exported instruction(s).`);
    }
}
