import { describe, expect, it } from 'vitest';
import { waitFor } from '#src/shared/wait-for.js';

describe('waitFor', () => {
    it('resolves once the condition becomes true', async () => {
        let ready = false;
        setTimeout(() => { ready = true; }, 20);
        await waitFor(() => ready, 500, 5);
        expect(ready).toBe(true);
    });

    it('returns immediately when the condition is already true', async () => {
        const start = Date.now();
        await waitFor(() => true);
        expect(Date.now() - start).toBeLessThan(50);
    });

    it('throws a timeout error when the condition never becomes true', async () => {
        await expect(waitFor(() => false, 30, 5)).rejects.toThrow(/waitFor timeout/);
    });
});
