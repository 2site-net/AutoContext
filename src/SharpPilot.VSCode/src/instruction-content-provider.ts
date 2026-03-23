import * as vscode from 'vscode';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { parseRules } from './rule-parser.js';
import type { SharpPilotConfigManager } from './sharppilot-config.js';

export const instructionScheme = 'sharp-pilot-instructions';

export class InstructionContentProvider implements vscode.TextDocumentContentProvider, vscode.Disposable {
    private readonly didChangeEmitter = new vscode.EventEmitter<vscode.Uri>();
    readonly onDidChange = this.didChangeEmitter.event;
    private readonly disposables: vscode.Disposable[] = [];

    constructor(
        private readonly extensionPath: string,
        private readonly configManager: SharpPilotConfigManager,
    ) {
        this.disposables.push(
            this.didChangeEmitter,
            configManager.onDidChange(() => this.refresh()),
        );
    }

    provideTextDocumentContent(uri: vscode.Uri): string {
        const fileName = uri.path;
        const filePath = join(this.extensionPath, 'instructions', fileName);

        let content: string;
        try {
            content = readFileSync(filePath, 'utf-8');
        } catch {
            return `Unable to read instruction file: ${fileName}`;
        }

        const disabledHashes = this.configManager.getDisabledRules(fileName);
        if (disabledHashes.size === 0) {
            return content;
        }

        const rules = parseRules(content);
        const lines = content.split('\n');

        // Apply [DISABLED] tags in reverse order to preserve line indices.
        for (let i = rules.length - 1; i >= 0; i--) {
            const rule = rules[i];
            if (!disabledHashes.has(rule.hash)) {
                continue;
            }

            const line = lines[rule.startLine];
            const match = line.match(/^([-*]\s)(\*\*(?:Do|Don't)\*\*)/);
            if (match) {
                lines[rule.startLine] = `${match[1]}**[DISABLED]** ${match[2]}${line.slice(match[0].length)}`;
            }
        }

        return lines.join('\n');
    }

    refresh(): void {
        // Fire change for all open virtual documents with our scheme.
        for (const doc of vscode.workspace.textDocuments) {
            if (doc.uri.scheme === instructionScheme) {
                this.didChangeEmitter.fire(doc.uri);
            }
        }
    }

    buildUri(fileName: string): vscode.Uri {
        return vscode.Uri.from({ scheme: instructionScheme, path: fileName });
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }
}
