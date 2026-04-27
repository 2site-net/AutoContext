import { describe, it, expect } from 'vitest';
import { CorrelationScope } from '../../src/logging/correlation-scope.js';
import { Logger } from '../../src/logging/logger.js';
import type { LogRecord } from '../../src/logging/log-record.js';
import type { LogSink } from '../../src/logging/log-server-client.js';

class BufferSink implements LogSink {
    readonly records: LogRecord[] = [];
    enqueue(record: LogRecord): void {
        this.records.push(record);
    }
}

describe('Logger', () => {
    it('caches the per-category logger instance', () => {
        const logger = new Logger(new BufferSink());
        expect(logger.forCategory('A')).toBe(logger.forCategory('A'));
        expect(logger.forCategory('A')).not.toBe(logger.forCategory('B'));
    });

    it('routes each method to the matching .NET LogLevel name', () => {
        const sink = new BufferSink();
        const log = new Logger(sink).forCategory('Cat');
        log.trace('a');
        log.debug('b');
        log.info('c');
        log.warn('d');
        log.error('e');
        expect(sink.records.map((r) => r.level)).toEqual([
            'Trace',
            'Debug',
            'Information',
            'Warning',
            'Error',
        ]);
    });

    it('captures the active correlation id at emission time', async () => {
        const sink = new BufferSink();
        const log = new Logger(sink).forCategory('Cat');

        log.info('outside');
        await CorrelationScope.run('abcd1234', () => {
            log.info('inside');
            return Promise.resolve();
        });
        log.info('after');

        expect(sink.records.map((r) => r.correlationId)).toEqual([undefined, 'abcd1234', undefined]);
    });

    it('formats Error exceptions using the stack', () => {
        const sink = new BufferSink();
        const err = new Error('boom');
        new Logger(sink).forCategory('Cat').error('fail', err);
        const record = sink.records[0];
        expect(record).toBeDefined();
        expect(record?.exception).toContain('boom');
    });

    it('formats null exceptions as the string "null"', () => {
        const sink = new BufferSink();
        new Logger(sink).forCategory('Cat').error('fail', null);
        expect(sink.records[0]?.exception).toBe('null');
    });
});
