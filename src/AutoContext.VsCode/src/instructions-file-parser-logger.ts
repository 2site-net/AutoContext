import * as vscode from 'vscode';
import { join } from 'node:path';
import type { InstructionsFilesManifest } from './instructions-files-manifest.js';
import { InstructionsFileParser } from './instructions-file-parser.js';
import type { AutoContextConfigManager } from './autocontext-config.js';

export class InstructionsFileParserLogger {
    static async log(outputChannel: vscode.OutputChannel, extensionPath: string, configManager: AutoContextConfigManager, manifest: InstructionsFilesManifest): Promise<void> {
        outputChannel.clear();
        const config = await configManager.read();
        const warnOnMissingId = config.diagnostic?.warnOnMissingId === true;

        for (const entry of manifest.instructions) {
            let diagnostics;
            try {
                ({ result: { diagnostics } } = await InstructionsFileParser.fromFile(join(extensionPath, 'instructions', entry.name)));
            } catch (err) {
                outputChannel.appendLine(`[Instructions] Failed to parse ${entry.name}: ${err instanceof Error ? err.message : err}`);
                continue;
            }

            for (const d of diagnostics) {
                if (d.kind === 'missing-id' && !warnOnMissingId) {
                    continue;
                }

                outputChannel.appendLine(`[warn] ${entry.name}:${d.line + 1} — ${d.message}`);
            }
        }
    }
}
