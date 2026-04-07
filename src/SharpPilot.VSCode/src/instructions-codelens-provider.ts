import * as vscode from 'vscode';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { InstructionsParser } from './instructions-parser.js';
import type { InstructionsCatalog } from './instructions-catalog.js';
import { ContextKeys } from './context-keys.js';
import { instructionScheme } from './instructions-content-provider.js';
import type { SharpPilotConfigManager } from './sharppilot-config.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';

export const toggleInstructionCommandId = 'sharppilot.toggleInstruction';
export const resetInstructionsCommandId = 'sharppilot.resetInstructions';

export class InstructionsCodeLensProvider implements vscode.CodeLensProvider, vscode.Disposable {
    private readonly didChangeEmitter = new vscode.EventEmitter<void>();
    readonly onDidChangeCodeLenses = this.didChangeEmitter.event;
    private readonly disposables: vscode.Disposable[] = [];

    constructor(
        private readonly extensionPath: string,
        private readonly configManager: SharpPilotConfigManager,
        private readonly detector: WorkspaceContextDetector,
        private readonly catalog: InstructionsCatalog,
    ) {
        this.disposables.push(
            this.didChangeEmitter,
            configManager.onDidChange(() => this.didChangeEmitter.fire()),
            detector.onDidDetect(() => this.didChangeEmitter.fire()),
        );
    }

    provideCodeLenses(document: vscode.TextDocument): vscode.CodeLens[] {
        if (document.uri.scheme !== instructionScheme) {
            return [];
        }

        const fileName = document.uri.path;

        const entry = this.catalog.findByFileName(fileName);
        if (entry) {
            const ctxKeys = ContextKeys.forEntry(entry);
            if (ctxKeys.length > 0 && !ctxKeys.some(k => this.detector.get(k))) {
                return [];
            }

            if (this.detector.getOverriddenSettingIds().has(entry.settingId)) {
                return [];
            }
        }

        const filePath = join(this.extensionPath, 'instructions', fileName);

        let content: string;
        try {
            content = readFileSync(filePath, 'utf-8');
        } catch {
            return [];
        }

        const { instructions } = InstructionsParser.parse(content);
        const disabledIds = this.configManager.getDisabledInstructions(fileName);

        const lenses: vscode.CodeLens[] = [];

        if (disabledIds.size > 0) {
            lenses.push(new vscode.CodeLens(new vscode.Range(0, 0, 0, 0), {
                title: '$(refresh) Reset All Instructions',
                command: resetInstructionsCommandId,
                arguments: [fileName],
            }));
        }

        for (const instruction of instructions) {
            if (instruction.id === undefined) {
                continue;
            }

            const isDisabled = disabledIds.has(instruction.id);
            const range = new vscode.Range(instruction.startLine, 0, instruction.startLine, 0);

            lenses.push(new vscode.CodeLens(range, {
                title: isDisabled ? '$(check) Enable Instruction' : '$(x) Disable Instruction',
                command: toggleInstructionCommandId,
                arguments: [fileName, instruction.id],
            }));
        }

        return lenses;
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }
}
