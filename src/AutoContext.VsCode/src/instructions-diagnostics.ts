import * as vscode from 'vscode';
import { join } from 'node:path';
import type { InstructionsCatalog } from './instructions-catalog.js';
import { InstructionsParser } from './instructions-parser.js';
import type { AutoContextConfigManager } from './autocontext-config.js';

export class InstructionsDiagnostics {
    static async log(outputChannel: vscode.OutputChannel, extensionPath: string, configManager: AutoContextConfigManager, catalog: InstructionsCatalog): Promise<void> {
        outputChannel.clear();
        const config = await configManager.read();
        const warnOnMissingId = config.diagnostic?.warnOnMissingId === true;

        for (const entry of catalog.all) {
            let diagnostics;
            try {
                ({ result: { diagnostics } } = await InstructionsParser.fromFile(join(extensionPath, 'instructions', entry.fileName)));
            } catch {
                continue;
            }

            for (const d of diagnostics) {
                if (d.kind === 'missing-id' && !warnOnMissingId) {
                    continue;
                }

                outputChannel.appendLine(`[warn] ${entry.fileName}:${d.line + 1} — ${d.message}`);
            }
        }
    }
}
