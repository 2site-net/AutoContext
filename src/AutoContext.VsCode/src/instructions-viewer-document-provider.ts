import * as vscode from 'vscode';
import { join } from 'node:path';
import { InstructionsFileParser } from './instructions-file-parser.js';
import type { AutoContextConfigManager } from './autocontext-config-manager.js';
import type { Logger } from '#types/logger.js';

export const instructionScheme = 'autocontext-instructions';

export class InstructionsViewerDocumentProvider implements vscode.TextDocumentContentProvider, vscode.Disposable {
    private readonly didChangeEmitter = new vscode.EventEmitter<vscode.Uri>();
    readonly onDidChange = this.didChangeEmitter.event;
    private readonly disposables: vscode.Disposable[] = [];

    constructor(
        private readonly extensionPath: string,
        private readonly configManager: AutoContextConfigManager,
        private readonly logger: Logger,
    ) {
        this.disposables.push(
            this.didChangeEmitter,
            configManager.onDidChange(() => this.refresh()),
        );
    }

    async provideTextDocumentContent(uri: vscode.Uri): Promise<string> {
        const fileName = uri.path;
        const filePath = join(this.extensionPath, 'instructions', fileName);

        let content: string;
        let instructions;
        try {
            ({ content, result: { instructions } } = await InstructionsFileParser.fromFile(filePath));
        } catch (err) {
            this.logger.error(`Failed to parse ${fileName}`, err);
            return `Unable to read instruction file: ${fileName}`;
        }

        const disabledIds = await this.configManager.getDisabledInstructions(fileName);
        if (disabledIds.size === 0) {
            return content;
        }

        const lines = content.split('\n');

        // Apply [DISABLED] tags in reverse order to preserve line indices.
        for (let i = instructions.length - 1; i >= 0; i--) {
            const instruction = instructions[i];
            if (instruction.id === undefined || !disabledIds.has(instruction.id)) {
                continue;
            }

            const line = lines[instruction.startLine];
            const match = line.match(/^([-*]\s)(?:\[INST\d{4}\]\s*)?(\*\*(?:Do|Don't)\*\*)/);
            if (match) {
                lines[instruction.startLine] = `${match[1]}**[DISABLED]** ${match[2]}${line.slice(match[0].length)}`;
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
