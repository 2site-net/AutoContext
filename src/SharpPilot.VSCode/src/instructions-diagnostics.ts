import * as vscode from 'vscode';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { InstructionsRegistry } from './instructions-registry.js';
import { InstructionsParser } from './instructions-parser.js';
import type { SharpPilotConfigManager } from './sharppilot-config.js';

export class InstructionsDiagnostics {
    static log(outputChannel: vscode.OutputChannel, extensionPath: string, configManager: SharpPilotConfigManager): void {
        outputChannel.clear();
        const warnOnMissingId = configManager.read().diagnostic?.warnOnMissingId === true;

        for (const entry of InstructionsRegistry.all) {
            let content: string;
            try {
                content = readFileSync(join(extensionPath, 'instructions', entry.fileName), 'utf-8');
            } catch {
                continue;
            }

            const { diagnostics } = InstructionsParser.parse(content);

            for (const d of diagnostics) {
                if (d.kind === 'missing-id' && !warnOnMissingId) {
                    continue;
                }

                outputChannel.appendLine(`[warn] ${entry.fileName}:${d.line + 1} — ${d.message}`);
            }
        }
    }
}
