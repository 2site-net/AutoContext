import { describe, it, expect, vi, beforeEach } from 'vitest';
import { InstructionsFilesDiagnosticsReporter } from '../../src/instructions-files-diagnostics-reporter';
import type {
    InstructionsFilesDiagnosticRecord,
    InstructionsFilesDiagnosticsRunner,
} from '../../src/instructions-files-diagnostics-runner';
import type * as vscode from 'vscode';

vi.mock('vscode', async () => await import('./_fakes/fake-vscode'));

function fakeChannel(): vscode.OutputChannel {
    return {
        name: 'test',
        append: vi.fn(),
        appendLine: vi.fn(),
        clear: vi.fn(),
        dispose: vi.fn(),
        hide: vi.fn(),
        show: vi.fn(),
        replace: vi.fn(),
    } as unknown as vscode.OutputChannel;
}

function fakeRunner(records: InstructionsFilesDiagnosticRecord[]): InstructionsFilesDiagnosticsRunner {
    return { collect: vi.fn(async () => records) } as unknown as InstructionsFilesDiagnosticsRunner;
}

beforeEach(() => {
    vi.clearAllMocks();
});

describe('InstructionsFilesDiagnosticsReporter.report', () => {
    it('should clear the channel before writing new records', async () => {
        const channel = fakeChannel();
        const reporter = new InstructionsFilesDiagnosticsReporter(fakeRunner([]), channel);

        await reporter.report();

        expect(channel.clear).toHaveBeenCalledOnce();
        expect(channel.appendLine).not.toHaveBeenCalled();
    });

    it('should format parse-error records with the [Instructions] prefix', async () => {
        const channel = fakeChannel();
        const reporter = new InstructionsFilesDiagnosticsReporter(
            fakeRunner([{ kind: 'parse-error', entry: 'a.instructions.md', message: 'boom' }]),
            channel,
        );

        await reporter.report();

        expect(channel.appendLine).toHaveBeenCalledExactlyOnceWith(
            '[Instructions] Failed to parse a.instructions.md: boom',
        );
    });

    it('should format parser diagnostics with [warn] prefix and 1-based line', async () => {
        const channel = fakeChannel();
        const reporter = new InstructionsFilesDiagnosticsReporter(
            fakeRunner([
                { kind: 'duplicate-id', entry: 'a.instructions.md', line: 4, message: 'dup' },
                { kind: 'malformed-id', entry: 'b.instructions.md', line: 0, message: 'bad' },
            ]),
            channel,
        );

        await reporter.report();

        expect(channel.appendLine).toHaveBeenNthCalledWith(1, '[warn] a.instructions.md:5 — dup');
        expect(channel.appendLine).toHaveBeenNthCalledWith(2, '[warn] b.instructions.md:1 — bad');
    });

    it('should delegate collection to the injected runner', async () => {
        const runner = fakeRunner([]);
        const reporter = new InstructionsFilesDiagnosticsReporter(runner, fakeChannel());

        await reporter.report();

        expect(runner.collect).toHaveBeenCalledOnce();
    });

    it('should not dispose an externally-injected channel', () => {
        const channel = fakeChannel();
        const reporter = new InstructionsFilesDiagnosticsReporter(fakeRunner([]), channel);

        reporter.dispose();

        expect(channel.dispose).not.toHaveBeenCalled();
    });

    it('should dispose the owned channel when none is injected', async () => {
        const fakeVscode = await import('./_fakes/fake-vscode');
        const ownedChannel = fakeChannel();
        vi.mocked(fakeVscode.window.createOutputChannel).mockReturnValueOnce(ownedChannel);

        const reporter = new InstructionsFilesDiagnosticsReporter(fakeRunner([]));

        reporter.dispose();

        expect(ownedChannel.dispose).toHaveBeenCalledOnce();
    });
});
