import * as vscode from 'vscode';
import type { ExportManifest } from './config';

export const manifestRelativePath = '.github/.sharp-pilot-exports.json';

export async function readManifest(uri: vscode.Uri): Promise<ExportManifest | undefined> {
    try {
        const bytes = await vscode.workspace.fs.readFile(uri);
        return JSON.parse(new TextDecoder().decode(bytes)) as ExportManifest;
    } catch {
        return undefined;
    }
}

export async function writeManifest(uri: vscode.Uri, manifest: ExportManifest): Promise<void> {
    const content = new TextEncoder().encode(JSON.stringify(manifest, null, 2) + '\n');

    await vscode.workspace.fs.writeFile(uri, content);
}
