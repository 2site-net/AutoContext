import * as vscode from 'vscode';
import type { InstructionsFilesDiagnosticsRunner } from './instructions-files-diagnostics-runner.js';

/**
 * Output-channel sink for instruction-file diagnostics. Owns a dedicated
 * `'AutoContext: Instructions'` output channel so `clear()` only wipes
 * diagnostic lines — never unrelated activation/worker output on the
 * shared `'AutoContext'` channel. Collects records via the injected
 * {@link InstructionsFilesDiagnosticsRunner} and formats one line per
 * record; kept as a thin presentation layer so the runner stays free of
 * any VS Code dependency.
 */
export class InstructionsFilesDiagnosticsReporter implements vscode.Disposable {
    private readonly outputChannel: vscode.OutputChannel;
    private readonly ownsChannel: boolean;

    constructor(
        private readonly runner: InstructionsFilesDiagnosticsRunner,
        outputChannel?: vscode.OutputChannel,
    ) {
        this.ownsChannel = outputChannel === undefined;
        this.outputChannel = outputChannel ?? vscode.window.createOutputChannel('AutoContext: Instructions');
    }

    async report(): Promise<void> {
        this.outputChannel.clear();
        const records = await this.runner.collect();

        for (const record of records) {
            if (record.kind === 'parse-error') {
                this.outputChannel.appendLine(`[Instructions] Failed to parse ${record.entry}: ${record.message}`);
            } else {
                this.outputChannel.appendLine(`[warn] ${record.entry}:${record.line + 1} — ${record.message}`);
            }
        }
    }

    dispose(): void {
        if (this.ownsChannel) {
            this.outputChannel.dispose();
        }
    }
}
