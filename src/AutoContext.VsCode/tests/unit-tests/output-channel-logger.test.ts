import { describe, it, expect, vi, beforeEach } from 'vitest';
import { OutputChannelLogger } from '#src/output-channel-logger';
import { LogCategory } from 'autocontext-nodejs-core';
import type * as vscode from 'vscode';

vi.mock('vscode', async () => await import('#testing/fakes/fake-vscode'));

function fakeChannel(name = 'test'): vscode.LogOutputChannel {
    return {
        name,
        logLevel: 1, // vscode.LogLevel.Trace — permissive so write() always reaches the spies
        onDidChangeLogLevel: vi.fn(),
        append: vi.fn(),
        appendLine: vi.fn(),
        replace: vi.fn(),
        clear: vi.fn(),
        show: vi.fn(),
        hide: vi.fn(),
        dispose: vi.fn(),
        trace: vi.fn(),
        debug: vi.fn(),
        info: vi.fn(),
        warn: vi.fn(),
        error: vi.fn(),
    } as unknown as vscode.LogOutputChannel;
}

beforeEach(() => {
    vi.clearAllMocks();
});

describe('OutputChannelLogger', () => {
    it('should forward info to the channel info method', () => {
        const channel = fakeChannel();
        const logger = new OutputChannelLogger(channel);

        logger.info('hello');

        expect(channel.info).toHaveBeenCalledExactlyOnceWith('hello');
    });

    it('should prepend the category when set', () => {
        const channel = fakeChannel();
        const logger = new OutputChannelLogger(channel, LogCategory.Config);

        logger.warn('something off');

        expect(channel.warn).toHaveBeenCalledExactlyOnceWith('[Config] something off');
    });

    it('should pass an Error instance through as a trailing argument so the channel can render its stack', () => {
        const channel = fakeChannel();
        const logger = new OutputChannelLogger(channel, LogCategory.Diagnostics);
        const error = new Error('parse failed');

        logger.error('Failed to log diagnostics', error);

        expect(channel.error).toHaveBeenCalledExactlyOnceWith('[Diagnostics] Failed to log diagnostics', error);
    });

    it('should pass non-Error error values through as a trailing argument', () => {
        const channel = fakeChannel();
        const logger = new OutputChannelLogger(channel);

        logger.error('one', 'plain string');
        logger.error('two', 42);
        logger.error('three', null);

        expect(channel.error).toHaveBeenNthCalledWith(1, 'one', 'plain string');
        expect(channel.error).toHaveBeenNthCalledWith(2, 'two', 42);
        expect(channel.error).toHaveBeenNthCalledWith(3, 'three', null);
    });

    it('should route every level to the matching channel method', () => {
        const channel = fakeChannel();
        const logger = new OutputChannelLogger(channel);

        logger.trace('t');
        logger.debug('d');
        logger.info('i');
        logger.warn('w');
        logger.error('e');

        expect(channel.trace).toHaveBeenCalledExactlyOnceWith('t');
        expect(channel.debug).toHaveBeenCalledExactlyOnceWith('d');
        expect(channel.info).toHaveBeenCalledExactlyOnceWith('i');
        expect(channel.warn).toHaveBeenCalledExactlyOnceWith('w');
        expect(channel.error).toHaveBeenCalledExactlyOnceWith('e');
    });

    it('should produce, via forCategory, a child that writes to the same channel with a new category', () => {
        const channel = fakeChannel();
        const root = new OutputChannelLogger(channel);

        const child = root.forCategory(LogCategory.HealthMonitor);
        child.debug('connected');

        expect(channel.debug).toHaveBeenCalledExactlyOnceWith('[HealthMonitor] connected');
    });

    it('should accept a freeform string passed to forCategory for dynamic categories', () => {
        const channel = fakeChannel();
        const root = new OutputChannelLogger(channel);

        const child = root.forCategory('Worker.DotNet');
        child.info('started');

        expect(channel.info).toHaveBeenCalledExactlyOnceWith('[Worker.DotNet] started');
    });

    it('should replace, not append, the category when forCategory is called on a child', () => {
        const channel = fakeChannel();
        const root = new OutputChannelLogger(channel);

        root.forCategory(LogCategory.Config).forCategory(LogCategory.Diagnostics).info('m');

        expect(channel.info).toHaveBeenCalledExactlyOnceWith('[Diagnostics] m');
    });

    it('should clear the underlying channel when clear is invoked', () => {
        const channel = fakeChannel();
        const logger = new OutputChannelLogger(channel);

        logger.clear();

        expect(channel.clear).toHaveBeenCalledOnce();
    });

    it('should clear the channel a child writes to when clear is invoked on the child', () => {
        const root = fakeChannel('root');
        const sibling = fakeChannel('sibling');
        const factory = vi.fn(() => sibling);
        const logger = new OutputChannelLogger(root, undefined, undefined, factory);

        logger.forChannel('sibling').forCategory(LogCategory.Config).clear();

        expect(sibling.clear).toHaveBeenCalledOnce();
        expect(root.clear).not.toHaveBeenCalled();
    });

    it('should create a new channel on the first forChannel request and write to it', () => {
        const root = fakeChannel('root');
        const sibling = fakeChannel('sibling');
        const factory = vi.fn(() => sibling);
        const logger = new OutputChannelLogger(root, undefined, undefined, factory);

        logger.forChannel('sibling').info('hi');

        expect(factory).toHaveBeenCalledExactlyOnceWith('sibling');
        expect(sibling.info).toHaveBeenCalledExactlyOnceWith('hi');
        expect(root.info).not.toHaveBeenCalled();
    });

    it('should cache and return loggers backed by the same channel for repeated forChannel names', () => {
        const root = fakeChannel('root');
        const sibling = fakeChannel('sibling');
        const factory = vi.fn(() => sibling);
        const logger = new OutputChannelLogger(root, undefined, undefined, factory);

        logger.forChannel('sibling').info('first');
        logger.forChannel('sibling').warn('second');
        // calling from a derived child must hit the same cache
        logger.forCategory(LogCategory.Config).forChannel('sibling').error('third');

        expect(factory).toHaveBeenCalledOnce();
        expect(sibling.info).toHaveBeenCalledExactlyOnceWith('first');
        expect(sibling.warn).toHaveBeenCalledExactlyOnceWith('second');
        expect(sibling.error).toHaveBeenCalledExactlyOnceWith('third');
    });

    it('should return the root channel from the cache when forChannel is called with the root channel name', () => {
        const root = fakeChannel('AutoContext');
        const factory = vi.fn();
        const logger = new OutputChannelLogger(root, undefined, undefined, factory);

        logger.forChannel('AutoContext').info('echo');

        expect(factory).not.toHaveBeenCalled();
        expect(root.info).toHaveBeenCalledExactlyOnceWith('echo');
    });

    it('should dispose every channel created through the logger tree', () => {
        const root = fakeChannel('root');
        const a = fakeChannel('a');
        const b = fakeChannel('b');
        const factory = vi.fn((name: string) => (name === 'a' ? a : b));
        const logger = new OutputChannelLogger(root, undefined, undefined, factory);

        logger.forChannel('a');
        logger.forChannel('b');
        logger.dispose();

        expect(root.dispose).toHaveBeenCalledOnce();
        expect(a.dispose).toHaveBeenCalledOnce();
        expect(b.dispose).toHaveBeenCalledOnce();
    });
});
