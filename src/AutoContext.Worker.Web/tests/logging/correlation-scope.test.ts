import { describe, it, expect } from 'vitest';
import { CorrelationScope } from '#src/logging/correlation-scope.js';

describe('CorrelationScope', () => {
    it('returns undefined when no scope is active', () => {
        expect(CorrelationScope.current()).toBeUndefined();
    });

    it('exposes the id inside a run callback', async () => {
        const observed = await CorrelationScope.run('abc12345', () => {
            return Promise.resolve(CorrelationScope.current());
        });
        expect(observed).toBe('abc12345');
    });

    it('isolates concurrent dispatches', async () => {
        const observed = await Promise.all([
            CorrelationScope.run('id-a', async () => {
                await new Promise((r) => setTimeout(r, 10));
                return CorrelationScope.current();
            }),
            CorrelationScope.run('id-b', async () => {
                await new Promise((r) => setTimeout(r, 5));
                return CorrelationScope.current();
            }),
        ]);
        expect(observed).toEqual(['id-a', 'id-b']);
    });

    it('restores the previous id after the callback resolves', async () => {
        await CorrelationScope.run('outer', async () => {
            await CorrelationScope.run('inner', () => Promise.resolve());
            expect(CorrelationScope.current()).toBe('outer');
        });
        expect(CorrelationScope.current()).toBeUndefined();
    });

    it('throws on an empty id', () => {
        expect(() => CorrelationScope.run('', () => Promise.resolve())).toThrow(/non-empty/);
    });
});
