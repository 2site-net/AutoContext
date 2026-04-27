import { describe, it, expect } from 'vitest';
import { CorrelationScope } from '#src/logging/correlation-scope.js';
import { PipeLogger } from '#src/logging/logger.js';
import { FakeLoggingClient } from '#testing/fakes/fake-logging-client.js';

describe('PipeLogger', () => {
    it('caches the per-category logger instance across the tree', () => {
        const logger = new PipeLogger(new FakeLoggingClient());

        expect(logger.forCategory('A')).toBe(logger.forCategory('A'));
        expect(logger.forCategory('A')).not.toBe(logger.forCategory('B'));
        // Cache is shared with derived loggers, so a child can find a sibling category.
        expect(logger.forCategory('A').forCategory('B')).toBe(logger.forCategory('B'));
    });

    it('forCategory on a child replaces (does not append) the existing category', () => {
        const client = new FakeLoggingClient();
        const root = new PipeLogger(client);

        root.forCategory('Outer').forCategory('Inner').info('hi');

        expect(client.records[0]?.category).toBe('Inner');
    });

    it('routes each method to the matching .NET LogLevel name', () => {
        const client = new FakeLoggingClient();
        const log = new PipeLogger(client).forCategory('Cat');

        log.trace('a');
        log.debug('b');
        log.info('c');
        log.warn('d');
        log.error('e');

        expect(client.records.map((r) => r.level)).toEqual([
            'Trace',
            'Debug',
            'Information',
            'Warning',
            'Error',
        ]);
    });

    it('captures the active correlation id at emission time', async () => {
        const client = new FakeLoggingClient();
        const log = new PipeLogger(client).forCategory('Cat');

        log.info('outside');
        await CorrelationScope.run('abcd1234', () => {
            log.info('inside');
            return Promise.resolve();
        });
        log.info('after');

        expect(client.records.map((r) => r.correlationId)).toEqual([undefined, 'abcd1234', undefined]);
    });

    it('formats Error exceptions using the stack', () => {
        const client = new FakeLoggingClient();
        const err = new Error('boom');

        new PipeLogger(client).forCategory('Cat').error('fail', err);

        const record = client.records[0];
        expect(record).toBeDefined();
        expect(record?.exception).toContain('boom');
    });

    it('formats null exceptions as the string "null"', () => {
        const client = new FakeLoggingClient();

        new PipeLogger(client).forCategory('Cat').error('fail', null);

        expect(client.records[0]?.exception).toBe('null');
    });
});
