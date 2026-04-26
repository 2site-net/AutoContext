import { describe, it, expect, vi, beforeEach } from 'vitest';
import { OutputChannelLogger } from '../../src/output-channel-logger';
import { LogLevel, LogCategory } from '../../src/types/logger';
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

beforeEach(() => {
    vi.clearAllMocks();
});

describe('OutputChannelLogger', () => {
    it('should write a level-prefixed line for info', () => {
        const channel = fakeChannel();
        const logger = new OutputChannelLogger(channel);

        logger.info('hello');

        expect(channel.appendLine).toHaveBeenCalledWith('[info] hello');
    });

    it('should include the category when set', () => {
        const channel = fakeChannel();
        const logger = new OutputChannelLogger(channel, LogCategory.Config);

        logger.warn('something off');

        expect(channel.appendLine).toHaveBeenCalledWith('[warn] [Config] something off');
    });

    it('should append the error stack after the message when present', () => {
        const channel = fakeChannel();
        const logger = new OutputChannelLogger(channel, LogCategory.Diagnostics);
        const error = new Error('parse failed');
        error.stack = 'Error: parse failed\n    at foo';

        logger.error('Failed to log diagnostics', error);

        expect(channel.appendLine).toHaveBeenCalledWith(
            '[error] [Diagnostics] Failed to log diagnostics: Error: parse failed\n    at foo',
        );
    });

    it('should fall back to the error message when stack is missing', () => {
        const channel = fakeChannel();
        const logger = new OutputChannelLogger(channel);
        const error = new Error('parse failed');
        error.stack = undefined;

        logger.error('Failed', error);

        expect(channel.appendLine).toHaveBeenCalledWith('[error] Failed: parse failed');
    });

    it('should coerce non-Error error values via String', () => {
        const channel = fakeChannel();
        const logger = new OutputChannelLogger(channel);

        logger.error('one', 'plain string');
        logger.error('two', 42);
        logger.error('three', null);

        expect(channel.appendLine).toHaveBeenNthCalledWith(1, '[error] one: plain string');
        expect(channel.appendLine).toHaveBeenNthCalledWith(2, '[error] two: 42');
        expect(channel.appendLine).toHaveBeenNthCalledWith(3, '[error] three: null');
    });

    it('should suppress lines below the configured minimum level', () => {
        const channel = fakeChannel();
        const logger = new OutputChannelLogger(channel, undefined, LogLevel.Warn);

        logger.trace('t');
        logger.debug('d');
        logger.info('i');
        logger.warn('w');
        logger.error('e');

        expect(channel.appendLine).toHaveBeenCalledTimes(2);
        expect(channel.appendLine).toHaveBeenNthCalledWith(1, '[warn] w');
        expect(channel.appendLine).toHaveBeenNthCalledWith(2, '[error] e');
    });

    it('should suppress every line when level is Off', () => {
        const channel = fakeChannel();
        const logger = new OutputChannelLogger(channel, undefined, LogLevel.Off);

        logger.error('e');

        expect(channel.appendLine).not.toHaveBeenCalled();
    });

    it('forCategory should produce a child that writes to the same channel with a new category', () => {
        const channel = fakeChannel();
        const root = new OutputChannelLogger(channel, undefined, LogLevel.Trace);

        const child = root.forCategory(LogCategory.HealthMonitor);
        child.debug('connected');

        expect(channel.appendLine).toHaveBeenCalledWith('[debug] [HealthMonitor] connected');
    });

    it('forCategory should accept a freeform string for dynamic categories', () => {
        const channel = fakeChannel();
        const root = new OutputChannelLogger(channel);

        const child = root.forCategory('Worker.DotNet');
        child.info('started');

        expect(channel.appendLine).toHaveBeenCalledWith('[info] [Worker.DotNet] started');
    });

    it('forCategory called on a child should replace, not append, the category', () => {
        const channel = fakeChannel();
        const root = new OutputChannelLogger(channel);

        root.forCategory(LogCategory.Config).forCategory(LogCategory.Diagnostics).info('m');

        expect(channel.appendLine).toHaveBeenCalledWith('[info] [Diagnostics] m');
    });

    it('forCategory should propagate the minimum level to children', () => {
        const channel = fakeChannel();
        const root = new OutputChannelLogger(channel, undefined, LogLevel.Error);

        const child = root.forCategory(LogCategory.WorkerManager);
        child.info('ignored');
        child.error('kept');

        expect(channel.appendLine).toHaveBeenCalledOnce();
        expect(channel.appendLine).toHaveBeenCalledWith('[error] [WorkerManager] kept');
    });
});
