import { describe, it, expect, vi, beforeEach } from 'vitest';
import { InstructionsFilesDiagnosticsReporter } from '../../src/instructions-files-diagnostics-reporter';
import type {
    InstructionsFilesDiagnosticRecord,
    InstructionsFilesDiagnosticsRunner,
} from '../../src/instructions-files-diagnostics-runner';
import { createFakeLogger } from './_fakes';

vi.mock('vscode', async () => await import('./_fakes/fake-vscode'));

function fakeRunner(records: InstructionsFilesDiagnosticRecord[]): InstructionsFilesDiagnosticsRunner {
    return { collect: vi.fn(async () => records) } as unknown as InstructionsFilesDiagnosticsRunner;
}

beforeEach(() => {
    vi.clearAllMocks();
});

describe('InstructionsFilesDiagnosticsReporter.report', () => {
    it('should log an empty-result info line and nothing else when there are no records', async () => {
        const logger = createFakeLogger();
        const reporter = new InstructionsFilesDiagnosticsReporter(fakeRunner([]), logger);

        await reporter.report();

        expect(logger.info).toHaveBeenCalledExactlyOnceWith('No instruction-file diagnostics to report.');
        expect(logger.warn).not.toHaveBeenCalled();
        expect(logger.error).not.toHaveBeenCalled();
    });

    it('should emit a header followed by an error line for parse-error records', async () => {
        const logger = createFakeLogger();
        const reporter = new InstructionsFilesDiagnosticsReporter(
            fakeRunner([{ kind: 'parse-error', entry: 'a.instructions.md', message: 'boom' }]),
            logger,
        );

        await reporter.report();

        expect(logger.info).toHaveBeenCalledExactlyOnceWith('Reporting 1 instruction-file diagnostic:');
        expect(logger.error).toHaveBeenCalledExactlyOnceWith('Failed to parse a.instructions.md: boom');
        expect(logger.warn).not.toHaveBeenCalled();
    });

    it('should emit warn lines with 1-based line numbers for parser diagnostics', async () => {
        const logger = createFakeLogger();
        const reporter = new InstructionsFilesDiagnosticsReporter(
            fakeRunner([
                { kind: 'duplicate-id', entry: 'a.instructions.md', line: 4, message: 'dup' },
                { kind: 'malformed-id', entry: 'b.instructions.md', line: 0, message: 'bad' },
            ]),
            logger,
        );

        await reporter.report();

        expect(logger.info).toHaveBeenCalledExactlyOnceWith('Reporting 2 instruction-file diagnostics:');
        expect(logger.warn).toHaveBeenNthCalledWith(1, 'a.instructions.md:5 — dup');
        expect(logger.warn).toHaveBeenNthCalledWith(2, 'b.instructions.md:1 — bad');
        expect(logger.error).not.toHaveBeenCalled();
    });

    it('should delegate collection to the injected runner', async () => {
        const runner = fakeRunner([]);
        const reporter = new InstructionsFilesDiagnosticsReporter(runner, createFakeLogger());

        await reporter.report();

        expect(runner.collect).toHaveBeenCalledOnce();
    });
});
