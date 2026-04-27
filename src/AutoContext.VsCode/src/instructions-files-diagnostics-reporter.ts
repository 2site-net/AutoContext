import type { InstructionsFilesDiagnosticsRunner } from './instructions-files-diagnostics-runner.js';
import { LogCategory, type Logger } from '#types/logger.js';

/**
 * Logger sink for instruction-file diagnostics. Owns a dedicated
 * `'AutoContext: Instructions'` output channel obtained via
 * {@link Logger.forChannel} so {@link Logger.clear} only wipes
 * diagnostic lines — never unrelated activation/worker output on the
 * shared `'AutoContext'` channel. The channel is cached and disposed
 * by the root logger; the reporter itself owns no resources.
 */
export class InstructionsFilesDiagnosticsReporter {
    private readonly logger: Logger;

    constructor(
        private readonly runner: InstructionsFilesDiagnosticsRunner,
        parentLogger: Logger,
    ) {
        this.logger = parentLogger.forChannel('AutoContext: Instructions').forCategory(LogCategory.Instructions);
    }

    async report(): Promise<void> {
        this.logger.clear();
        const records = await this.runner.collect();

        for (const record of records) {
            if (record.kind === 'parse-error') {
                this.logger.error(`Failed to parse ${record.entry}: ${record.message}`);
            } else {
                this.logger.warn(`${record.entry}:${record.line + 1} — ${record.message}`);
            }
        }
    }
}
