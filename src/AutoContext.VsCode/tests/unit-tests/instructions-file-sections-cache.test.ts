import { describe, it, expect, vi } from 'vitest';
import { InstructionsFileSectionsCache } from '#src/instructions-file-sections-cache';
import { InstructionsFileSectionsParser } from '#src/instructions-file-sections-parser';

describe('InstructionsFileSectionsCache', () => {
    it('should parse on first miss and return the same array reference on subsequent hits', () => {
        const cache = new InstructionsFileSectionsCache();
        const body = '## Alpha\n\ntext\n## Bravo\n';

        const first = cache.get(body);
        const second = cache.get(body);

        expect(first).toBe(second);
        expect(first.map(s => s.heading)).toEqual(['Alpha', 'Bravo']);
    });

    it('should key by body content, so different bodies do not collide', () => {
        const cache = new InstructionsFileSectionsCache();
        const a = cache.get('## A\n');
        const b = cache.get('## B\n');

        expect(a).not.toBe(b);
        expect(a[0].heading).toBe('A');
        expect(b[0].heading).toBe('B');
    });

    it('should avoid re-parsing on a hit', () => {
        const parseSpy = vi.spyOn(InstructionsFileSectionsParser, 'parse');
        const cache = new InstructionsFileSectionsCache();
        const body = '## Hit\n';

        cache.get(body);
        cache.get(body);
        cache.get(body);

        expect(parseSpy).toHaveBeenCalledTimes(1);
        parseSpy.mockRestore();
    });

    it('should evict the least-recently-used entry when capacity is exceeded', () => {
        const cache = new InstructionsFileSectionsCache(2);

        const a = cache.get('## A\n');
        cache.get('## B\n');
        cache.get('## C\n'); // evicts A

        expect(cache.size).toBe(2);

        const parseSpy = vi.spyOn(InstructionsFileSectionsParser, 'parse');
        const aReparsed = cache.get('## A\n');
        expect(parseSpy).toHaveBeenCalledTimes(1);
        // Same content → equivalent shape, but a fresh array because A was evicted.
        expect(aReparsed).not.toBe(a);
        expect(aReparsed[0].heading).toBe('A');
        parseSpy.mockRestore();
    });

    it('should promote a hit to most-recently-used so it survives the next eviction', () => {
        const cache = new InstructionsFileSectionsCache(2);

        cache.get('## A\n');
        cache.get('## B\n');
        cache.get('## A\n'); // refresh A → B is now LRU
        cache.get('## C\n'); // evicts B, not A

        const parseSpy = vi.spyOn(InstructionsFileSectionsParser, 'parse');
        cache.get('## A\n'); // hit
        cache.get('## B\n'); // miss → re-parsed
        expect(parseSpy).toHaveBeenCalledTimes(1);
        parseSpy.mockRestore();
    });

    it('should reject non-positive capacity', () => {
        expect(() => new InstructionsFileSectionsCache(0)).toThrow(/maxEntries/);
        expect(() => new InstructionsFileSectionsCache(-1)).toThrow(/maxEntries/);
    });
});
