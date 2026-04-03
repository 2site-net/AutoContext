import * as vscode from 'vscode';
import type { InstructionsCatalogEntry } from './instructions-catalog-entry.js';
import { InstructionsRegistry } from './instructions-registry.js';

export class InstructionsExportState {
    static async getUnexportedFiles(
        entries: readonly InstructionsCatalogEntry[] = InstructionsRegistry.all,
    ): Promise<readonly InstructionsCatalogEntry[]> {
        const rootUri = vscode.workspace.workspaceFolders?.[0]?.uri;
        if (!rootUri) {
            return entries;
        }

        const checks = await Promise.all(entries.map(async entry => ({
            entry,
            exported: await InstructionsExportState.fileExists(vscode.Uri.joinPath(rootUri, entry.targetPath)),
        })));

        return checks.filter(c => !c.exported).map(c => c.entry);
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
