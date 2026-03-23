import * as vscode from 'vscode';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { parseRules } from './rule-parser.js';
import { instructionScheme } from './instruction-content-provider.js';
import type { SharpPilotConfigManager } from './sharppilot-config.js';

export class RuleDecorationManager implements vscode.Disposable {
    private readonly decorationType = vscode.window.createTextEditorDecorationType({ opacity: '0.4' });
    private readonly disposables: vscode.Disposable[] = [];

    constructor(
        private readonly extensionPath: string,
        private readonly configManager: SharpPilotConfigManager,
    ) {
        this.disposables.push(
            this.decorationType,
            vscode.window.onDidChangeActiveTextEditor(editor => {
                if (editor) {
                    this.applyDecorations(editor);
                }
            }),
            configManager.onDidChange(() => this.refreshAll()),
        );
    }

    applyDecorations(editor: vscode.TextEditor): void {
        if (editor.document.uri.scheme !== instructionScheme) {
            return;
        }

        const fileName = editor.document.uri.path;
        const filePath = join(this.extensionPath, 'instructions', fileName);

        let content: string;
        try {
            content = readFileSync(filePath, 'utf-8');
        } catch {
            return;
        }

        const rules = parseRules(content);
        const disabledHashes = this.configManager.getDisabledRules(fileName);
        const ranges: vscode.Range[] = [];

        for (const rule of rules) {
            if (disabledHashes.has(rule.hash)) {
                ranges.push(new vscode.Range(rule.startLine, 0, rule.endLine, Number.MAX_SAFE_INTEGER));
            }
        }

        editor.setDecorations(this.decorationType, ranges);
    }

    refreshAll(): void {
        for (const editor of vscode.window.visibleTextEditors) {
            this.applyDecorations(editor);
        }
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }
}
