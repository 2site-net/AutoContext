import { describe, it, expect } from 'vitest';
import { hashContent, manifestRelativePath } from '../../src/export-manifest';

describe('manifestRelativePath', () => {
    it('should point to .github/.sharppilot-exports.json', () => {
        expect(manifestRelativePath).toBe('.github/.sharppilot-exports.json');
    });
});

describe('hashContent', () => {
    it('should return a 16-character hex string', () => {
        const hash = hashContent(new TextEncoder().encode('hello'));

        expect(hash).toMatch(/^[0-9a-f]{16}$/);
    });

    it('should be deterministic for the same input', () => {
        const bytes = new TextEncoder().encode('test content');

        expect(hashContent(bytes)).toBe(hashContent(bytes));
    });

    it('should differ for different inputs', () => {
        const a = hashContent(new TextEncoder().encode('input-a'));
        const b = hashContent(new TextEncoder().encode('input-b'));

        expect(a).not.toBe(b);
    });
});
