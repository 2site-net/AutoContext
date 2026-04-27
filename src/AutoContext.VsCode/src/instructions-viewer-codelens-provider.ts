import * as vscode from 'vscode';
import { join } from 'node:path';
import { InstructionsFileParser } from './instructions-file-parser.js';
import type { InstructionsFilesManifest } from './instructions-files-manifest.js';
import { instructionScheme } from './instructions-viewer-document-provider.js';
import type { AutoContextConfigManager } from './autocontext-config.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import { commandIds } from './ui-constants.js';
import type { Logger } from '#types/logger.js';

export class InstructionsViewerCodeLensProvider implements vscode.CodeLensProvider, vscode.Disposable {
    private readonly didChangeEmitter = new vscode.EventEmitter<void>();
    readonly onDidChangeCodeLenses = this.didChangeEmitter.event;
    private readonly disposables: vscode.Disposable[] = [];

    constructor(
        private readonly extensionPath: string,
        private readonly configManager: AutoContextConfigManager,
        private readonly detector: WorkspaceContextDetector,
        private readonly manifest: InstructionsFilesManifest,
        private readonly logger: Logger,
    ) {
        this.disposables.push(
            this.didChangeEmitter,
            configManager.onDidChange(() => this.didChangeEmitter.fire()),
            detector.onDidDetect(() => this.didChangeEmitter.fire()),
        );
    }

    async provideCodeLenses(document: vscode.TextDocument): Promise<vscode.CodeLens[]> {
        if (document.uri.scheme !== instructionScheme) {
            return [];
        }

        const fileName = document.uri.path;

        const entry = this.manifest.findByName(fileName);
        if (entry) {
            const flags = entry.activationFlags;
            if (flags.length > 0 && !flags.some(k => this.detector.get(k))) {
                return [];
            }

            if (this.detector.getOverriddenContextKeys().has(entry.runtimeInfo.contextKey)) {
                return [];
            }
        }

        const filePath = join(this.extensionPath, 'instructions', fileName);

        let instructions;
        try {
            ({ result: { instructions } } = await InstructionsFileParser.fromFile(filePath));
        } catch (err) {
            this.logger.error(`Failed to parse ${fileName}`, err);
            return [];
        }
        const disabledIds = await this.configManager.getDisabledInstructions(fileName);

        const lenses: vscode.CodeLens[] = [];

        if (disabledIds.size > 0) {
            lenses.push(new vscode.CodeLens(new vscode.Range(0, 0, 0, 0), {
                title: '$(refresh) Reset All Instructions',
                command: commandIds.ResetInstructions,
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
                command: commandIds.ToggleInstruction,
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
