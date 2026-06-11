import { createHash } from 'node:crypto';
import { InstructionsFileSectionsParser } from './instructions-file-sections-parser.js';
import type { InstructionsFileSectionWithOffsets } from '#types/instructions-file-section-with-offsets.js';

const DEFAULT_MAX_ENTRIES = 200;

/**
 * In-memory cache of parsed instructions-file sections, keyed by
 * `sha256(body)`. Different bodies produce different keys, so stale
 * entries naturally fall out of the LRU window without explicit
 * invalidation.
 *
 * Used by runtime consumers (LM-tool projectors) that may compute
 * the section index for the same body repeatedly — e.g. once per
 * tool invocation against a still-warm `.generated/` write.
 */
export class InstructionsFileSectionsCache {
    private readonly entries = new Map<string, readonly InstructionsFileSectionWithOffsets[]>();

    constructor(private readonly maxEntries: number = DEFAULT_MAX_ENTRIES) {
        if (maxEntries <= 0) {
            throw new Error(`maxEntries must be > 0, got ${maxEntries}`);
        }
    }

    /**
     * Returns the section index for the given body, computing and
     * memoizing on miss. Hit promotes the entry to most-recently-used.
     */
    get(body: string): readonly InstructionsFileSectionWithOffsets[] {
        const key = InstructionsFileSectionsCache.hash(body);
        const cached = this.entries.get(key);
        if (cached !== undefined) {
            // Refresh recency: re-insertion moves the entry to the tail.
            this.entries.delete(key);
            this.entries.set(key, cached);
            return cached;
        }

        const sections = InstructionsFileSectionsParser.parse(body);
        this.entries.set(key, sections);
        if (this.entries.size > this.maxEntries) {
            // Map iterators yield in insertion order; first key is the LRU.
            const lru = this.entries.keys().next().value;
            if (lru !== undefined) {
                this.entries.delete(lru);
            }
        }
        return sections;
    }

    /** Test/diagnostic helper. */
    get size(): number {
        return this.entries.size;
    }

    private static hash(body: string): string {
        return createHash('sha256').update(body, 'utf-8').digest('hex');
    }
}
