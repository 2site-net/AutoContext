import * as vscode from 'vscode';
import { instructions, targetPath, type InstructionEntry } from './instructions-catalog.js';

export async function getUnexportedInstructions(
    entries: readonly InstructionEntry[] = instructions,
): Promise<readonly InstructionEntry[]> {
    const rootUri = vscode.workspace.workspaceFolders?.[0]?.uri;
    if (!rootUri) {
        return entries;
    }

    const checks = await Promise.all(entries.map(async entry => ({
        entry,
        exported: await fileExists(vscode.Uri.joinPath(rootUri, targetPath(entry))),
    })));

    return checks.filter(c => !c.exported).map(c => c.entry);
}

async function fileExists(uri: vscode.Uri): Promise<boolean> {
    try {
        await vscode.workspace.fs.stat(uri);
        return true;
    } catch {
        return false;
    }
}
