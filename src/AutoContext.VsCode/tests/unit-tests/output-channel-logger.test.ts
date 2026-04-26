import { describe, it, expect, vi, beforeEach } from 'vitest';
import { OutputChannelLogger } from '../../src/output-channel-logger';
import { LogLevel, LogCategory } from '../../src/types/logger';
import type * as vscode from 'vscode';

vi.mock('vscode', async () => await import('./_fakes/fake-vscode'));

function fakeChannel(name = 'test'): vscode.OutputChannel {
    return {
        name,
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

    it('clear should clear the underlying channel', () => {
        const channel = fakeChannel();
        const logger = new OutputChannelLogger(channel);

        logger.clear();

        expect(channel.clear).toHaveBeenCalledOnce();
    });

    it('clear on a child should clear the channel that child writes to', () => {
        const root = fakeChannel('root');
        const sibling = fakeChannel('sibling');
        const factory = vi.fn(() => sibling);
        const logger = new OutputChannelLogger(root, undefined, LogLevel.Info, undefined, factory);

        logger.forChannel('sibling').forCategory(LogCategory.Config).clear();

        expect(sibling.clear).toHaveBeenCalledOnce();
        expect(root.clear).not.toHaveBeenCalled();
    });

    it('forChannel should create a new channel on first request and write to it', () => {
        const root = fakeChannel('root');
        const sibling = fakeChannel('sibling');
        const factory = vi.fn(() => sibling);
        const logger = new OutputChannelLogger(root, undefined, LogLevel.Info, undefined, factory);

        logger.forChannel('sibling').info('hi');

        expect(factory).toHaveBeenCalledExactlyOnceWith('sibling');
        expect(sibling.appendLine).toHaveBeenCalledWith('[info] hi');
        expect(root.appendLine).not.toHaveBeenCalled();
    });

    it('forChannel should cache and return loggers backed by the same channel for repeated names', () => {
        const root = fakeChannel('root');
        const sibling = fakeChannel('sibling');
        const factory = vi.fn(() => sibling);
        const logger = new OutputChannelLogger(root, undefined, LogLevel.Info, undefined, factory);

        logger.forChannel('sibling').info('first');
        logger.forChannel('sibling').warn('second');
        // calling from a derived child must hit the same cache
        logger.forCategory(LogCategory.Config).forChannel('sibling').error('third');

        expect(factory).toHaveBeenCalledOnce();
        expect(sibling.appendLine).toHaveBeenNthCalledWith(1, '[info] first');
        expect(sibling.appendLine).toHaveBeenNthCalledWith(2, '[warn] second');
        expect(sibling.appendLine).toHaveBeenNthCalledWith(3, '[error] third');
    });

    it('forChannel called with the root channel name should return the root channel from the cache', () => {
        const root = fakeChannel('AutoContext');
        const factory = vi.fn();
        const logger = new OutputChannelLogger(root, undefined, LogLevel.Info, undefined, factory);

        logger.forChannel('AutoContext').info('echo');

        expect(factory).not.toHaveBeenCalled();
        expect(root.appendLine).toHaveBeenCalledWith('[info] echo');
    });

    it('forChannel should propagate the minimum level to the new channel logger', () => {
        const root = fakeChannel('root');
        const sibling = fakeChannel('sibling');
        const factory = vi.fn(() => sibling);
        const logger = new OutputChannelLogger(root, undefined, LogLevel.Error, undefined, factory);

        const channelLogger = logger.forChannel('sibling');
        channelLogger.info('ignored');
        channelLogger.error('kept');

        expect(sibling.appendLine).toHaveBeenCalledExactlyOnceWith('[error] kept');
    });

    it('dispose should dispose every channel created through the logger tree', () => {
        const root = fakeChannel('root');
        const a = fakeChannel('a');
        const b = fakeChannel('b');
        const factory = vi.fn((name: string) => (name === 'a' ? a : b));
        const logger = new OutputChannelLogger(root, undefined, LogLevel.Info, undefined, factory);

        logger.forChannel('a');
        logger.forChannel('b');
        logger.dispose();

        expect(root.dispose).toHaveBeenCalledOnce();
        expect(a.dispose).toHaveBeenCalledOnce();
        expect(b.dispose).toHaveBeenCalledOnce();
    });
});
