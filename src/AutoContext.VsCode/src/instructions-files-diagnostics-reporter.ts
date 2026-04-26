import type { InstructionsFilesDiagnosticsRunner } from './instructions-files-diagnostics-runner.js';
import type { Logger } from './types/logger.js';

/**
 * Logger sink for instruction-file diagnostics. Collects records via
 * the injected {@link InstructionsFilesDiagnosticsRunner} and emits
 * one line per record through the unified extension {@link Logger}.
 *
 * Each `report()` opens with a header line so multiple runs remain
 * visually separable in the output channel — replaces the previous
 * dedicated channel + `clear()` approach, which both wiped history
 * and fragmented logging across two channels.
 */
export class InstructionsFilesDiagnosticsReporter {
    constructor(
        private readonly runner: InstructionsFilesDiagnosticsRunner,
        private readonly logger: Logger,
    ) {}

    async report(): Promise<void> {
        const records = await this.runner.collect();

        if (records.length === 0) {
            this.logger.info('No instruction-file diagnostics to report.');
            return;
        }

        this.logger.info(`Reporting ${records.length} instruction-file diagnostic${records.length === 1 ? '' : 's'}:`);
        for (const record of records) {
            if (record.kind === 'parse-error') {
                this.logger.error(`Failed to parse ${record.entry}: ${record.message}`);
            } else {
                this.logger.warn(`${record.entry}:${record.line + 1} — ${record.message}`);
            }
        }
    }
}
