import * as vscode from 'vscode';
import { join } from 'node:path';
import { InstructionsParser } from './instructions-parser.js';
import { instructionScheme } from './instructions-content-provider.js';
import type { SharpPilotConfigManager } from './sharppilot-config.js';

export class InstructionsDecorationManager implements vscode.Disposable {
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
                    void this.applyDecorations(editor);
                }
            }),
            configManager.onDidChange(() => this.refreshAll()),
        );
    }

    async applyDecorations(editor: vscode.TextEditor): Promise<void> {
        if (editor.document.uri.scheme !== instructionScheme) {
            return;
        }

        const fileName = editor.document.uri.path;
        const filePath = join(this.extensionPath, 'instructions', fileName);

        let instructions;
        try {
            ({ result: { instructions } } = await InstructionsParser.fromFile(filePath));
        } catch {
            return;
        }
        const disabledIds = await this.configManager.getDisabledInstructions(fileName);
        const ranges: vscode.Range[] = [];

        for (const instruction of instructions) {
            if (instruction.id !== undefined && disabledIds.has(instruction.id)) {
                ranges.push(new vscode.Range(instruction.startLine, 0, instruction.endLine, Number.MAX_SAFE_INTEGER));
            }
        }

        editor.setDecorations(this.decorationType, ranges);
    }

    refreshAll(): void {
        for (const editor of vscode.window.visibleTextEditors) {
            void this.applyDecorations(editor);
        }
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }
}
