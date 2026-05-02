import type { InstructionsFilesDiagnosticsRunner } from './instructions-files-diagnostics-runner.js';
import { LogCategory, type ChannelLogger } from 'autocontext-framework-web';

/**
 * ChannelLogger sink for instruction-file diagnostics. Owns a dedicated
 * `'AutoContext: Instructions'` output channel obtained via
 * {@link ChannelLogger.forChannel} so {@link ChannelLogger.clear} only wipes
 * diagnostic lines — never unrelated activation/worker output on the
 * shared `'AutoContext'` channel. The channel is cached and disposed
 * by the root logger; the reporter itself owns no resources.
 */
export class InstructionsFilesDiagnosticsReporter {
    private readonly logger: ChannelLogger;

    constructor(
        private readonly runner: InstructionsFilesDiagnosticsRunner,
        parentLogger: ChannelLogger,
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
