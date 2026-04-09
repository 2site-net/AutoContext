import * as vscode from 'vscode';
import { join } from 'node:path';
import type { InstructionsCatalog } from './instructions-catalog.js';
import { InstructionsParser } from './instructions-parser.js';
import type { SharpPilotConfigManager } from './sharppilot-config.js';

export class InstructionsDiagnostics {
    static log(outputChannel: vscode.OutputChannel, extensionPath: string, configManager: SharpPilotConfigManager, catalog: InstructionsCatalog): void {
        outputChannel.clear();
        const warnOnMissingId = configManager.read().diagnostic?.warnOnMissingId === true;

        for (const entry of catalog.all) {
            let diagnostics;
            try {
                ({ result: { diagnostics } } = InstructionsParser.fromFile(join(extensionPath, 'instructions', entry.fileName)));
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
