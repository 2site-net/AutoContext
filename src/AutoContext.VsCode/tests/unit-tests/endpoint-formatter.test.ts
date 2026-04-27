import { describe, it, expect } from 'vitest';
import { formatEndpoint } from '#src/endpoint-formatter';

describe('formatEndpoint', () => {
    it('produces the base name when no suffix is given', () => {
        expect(formatEndpoint('dotnet')).toBe('autocontext.worker-dotnet');
        expect(formatEndpoint('workspace')).toBe('autocontext.worker-workspace');
        expect(formatEndpoint('web')).toBe('autocontext.worker-web');
    });

    it('appends a non-empty suffix with a single dash separator', () => {
        expect(formatEndpoint('dotnet', 'abc123')).toBe('autocontext.worker-dotnet-abc123');
    });

    it('treats an empty or whitespace suffix as absent', () => {
        expect(formatEndpoint('dotnet', '')).toBe('autocontext.worker-dotnet');
        expect(formatEndpoint('dotnet', '   ')).toBe('autocontext.worker-dotnet');
        expect(formatEndpoint('dotnet', undefined)).toBe('autocontext.worker-dotnet');
    });

    it('trims surrounding whitespace on the suffix', () => {
        expect(formatEndpoint('dotnet', '  abc123  ')).toBe('autocontext.worker-dotnet-abc123');
    });

    it('throws when id is empty or whitespace', () => {
        expect(() => formatEndpoint('')).toThrow();
        expect(() => formatEndpoint('   ')).toThrow();
    });
});
