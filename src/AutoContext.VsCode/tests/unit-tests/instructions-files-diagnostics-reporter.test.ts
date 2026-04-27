import { describe, it, expect, vi, beforeEach } from 'vitest';
import { InstructionsFilesDiagnosticsReporter } from '../../src/instructions-files-diagnostics-reporter';
import type {
    InstructionsFilesDiagnosticRecord,
    InstructionsFilesDiagnosticsRunner,
} from '../../src/instructions-files-diagnostics-runner';
import { LogCategory } from '#types/logger.js';
import { createFakeLogger } from './_fakes';

vi.mock('vscode', async () => await import('./_fakes/fake-vscode'));

function fakeRunner(records: InstructionsFilesDiagnosticRecord[]): InstructionsFilesDiagnosticsRunner {
    return { collect: vi.fn(async () => records) } as unknown as InstructionsFilesDiagnosticsRunner;
}

beforeEach(() => {
    vi.clearAllMocks();
});

describe('InstructionsFilesDiagnosticsReporter', () => {
    it('should request its own channel and Instructions category from the parent logger', () => {
        const parent = createFakeLogger();

        new InstructionsFilesDiagnosticsReporter(fakeRunner([]), parent);

        expect(parent.forChannel).toHaveBeenCalledWith('AutoContext: Instructions');
        expect(parent.forCategory).toHaveBeenCalledWith(LogCategory.Instructions);
    });

    it('should clear the channel before writing new records', async () => {
        const logger = createFakeLogger();
        const reporter = new InstructionsFilesDiagnosticsReporter(fakeRunner([]), logger);

        await reporter.report();

        expect(logger.clear).toHaveBeenCalledOnce();
        expect(logger.warn).not.toHaveBeenCalled();
        expect(logger.error).not.toHaveBeenCalled();
    });

    it('should emit an error line for parse-error records', async () => {
        const logger = createFakeLogger();
        const reporter = new InstructionsFilesDiagnosticsReporter(
            fakeRunner([{ kind: 'parse-error', entry: 'a.instructions.md', message: 'boom' }]),
            logger,
        );

        await reporter.report();

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

        expect(logger.warn).toHaveBeenNthCalledWith(1, 'a.instructions.md:5 — dup');
        expect(logger.warn).toHaveBeenNthCalledWith(2, 'b.instructions.md:1 — bad');
        expect(logger.error).not.toHaveBeenCalled();
    });

    it('should clear before emitting any diagnostic lines', async () => {
        const logger = createFakeLogger();
        const callOrder: string[] = [];
        vi.mocked(logger.clear).mockImplementation(() => { callOrder.push('clear'); });
        vi.mocked(logger.warn).mockImplementation(() => { callOrder.push('warn'); });
        vi.mocked(logger.error).mockImplementation(() => { callOrder.push('error'); });
        const reporter = new InstructionsFilesDiagnosticsReporter(
            fakeRunner([
                { kind: 'parse-error', entry: 'a.instructions.md', message: 'boom' },
                { kind: 'duplicate-id', entry: 'b.instructions.md', line: 0, message: 'dup' },
            ]),
            logger,
        );

        await reporter.report();

        expect(callOrder).toEqual(['clear', 'error', 'warn']);
    });

    it('should delegate collection to the injected runner', async () => {
        const runner = fakeRunner([]);
        const reporter = new InstructionsFilesDiagnosticsReporter(runner, createFakeLogger());

        await reporter.report();

        expect(runner.collect).toHaveBeenCalledOnce();
    });
});
