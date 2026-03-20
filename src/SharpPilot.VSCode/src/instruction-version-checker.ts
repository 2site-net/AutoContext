import * as vscode from 'vscode';
import { instructions, targetPath, type InstructionEntry } from './config.js';
import { hashContent, manifestRelativePath, readManifest, writeManifest } from './export-manifest.js';

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

        const hashes = new Map<string, string>();

        for (const entry of instructions) {
            if (!manifest.exports[entry.fileName]) {
                continue;
            }

            const sourceUri = vscode.Uri.file(`${this.extensionPath}/instructions/${entry.fileName}`);
            const content = await vscode.workspace.fs.readFile(sourceUri);

            hashes.set(entry.fileName, hashContent(content));
        }

        const updated = instructions.filter(
            i => hashes.has(i.fileName) && hashes.get(i.fileName) !== manifest.exports[i.fileName].hash,
        );

        if (updated.length === 0) {
            return;
        }

        const action = await vscode.window.showInformationMessage(
            `SharpPilot: ${updated.length} exported instruction(s) have been updated.`,
            'View Details',
            'Acknowledge',
            'Dismiss',
        );

        if (action === 'View Details') {
            await this.viewDetails(workspaceFolder.uri, updated);
        } else if (action === 'Acknowledge') {
            await this.acknowledge(manifestUri, manifest, updated, hashes);
        }
    }

    private async viewDetails(rootUri: vscode.Uri, updated: readonly InstructionEntry[]): Promise<void> {
        const selected = await vscode.window.showQuickPick(
            updated.map(i => ({ label: i.label, description: i.category, entry: i })),
            { title: 'Updated instructions' },
        );

        if (!selected) {
            return;
        }

        const sourceUri = vscode.Uri.file(`${this.extensionPath}/instructions/${selected.entry.fileName}`);
        const exportedUri = vscode.Uri.joinPath(rootUri, targetPath(selected.entry));

        await vscode.commands.executeCommand('vscode.diff', exportedUri, sourceUri, `${selected.entry.label}: Exported ↔ Latest`);
    }

    private async acknowledge(
        manifestUri: vscode.Uri,
        manifest: { exports: Record<string, { hash: string }> },
        updated: readonly InstructionEntry[],
        hashes: ReadonlyMap<string, string>,
    ): Promise<void> {
        for (const entry of updated) {
            manifest.exports[entry.fileName] = { hash: hashes.get(entry.fileName)! };
        }

        await writeManifest(manifestUri, manifest);
        await vscode.window.showInformationMessage(`Acknowledged ${updated.length} instruction update(s).`);
    }
}
