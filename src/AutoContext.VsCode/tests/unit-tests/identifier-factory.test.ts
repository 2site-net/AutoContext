import { describe, it, expect } from 'vitest';
import { IdentifierFactory } from '#src/identifier-factory';

describe('IdentifierFactory.createInstanceId', () => {
    it('returns a 12-character lowercase hex string', () => {
        const id = IdentifierFactory.createInstanceId();
        expect(id).toMatch(/^[0-9a-f]{12}$/);
    });

    it('produces a fresh value on each call', () => {
        const ids = new Set<string>();
        for (let i = 0; i < 50; i++) {
            ids.add(IdentifierFactory.createInstanceId());
        }
        // 50 random 12-hex draws should never collide; tolerate one by chance.
        expect(ids.size).toBeGreaterThanOrEqual(49);
    });
});

describe('IdentifierFactory.createServiceAddress', () => {
    it('formats canonical autocontext.<role>#<instanceId>', () => {
        expect(IdentifierFactory.createServiceAddress('log', 'abc123def456'))
            .toBe('autocontext.log#abc123def456');
    });

    it('preserves multi-segment role names like worker-dotnet', () => {
        expect(IdentifierFactory.createServiceAddress('worker-dotnet', 'ffeeddccbbaa'))
            .toBe('autocontext.worker-dotnet#ffeeddccbbaa');
    });

    it('rejects an empty role', () => {
        expect(() => IdentifierFactory.createServiceAddress('', 'abc123def456'))
            .toThrow(/Service role must be a non-empty string/);
    });

    it('rejects a whitespace-only role', () => {
        expect(() => IdentifierFactory.createServiceAddress('   ', 'abc123def456'))
            .toThrow(/Service role must be a non-empty string/);
    });

    it('rejects an empty instance id', () => {
        expect(() => IdentifierFactory.createServiceAddress('log', ''))
            .toThrow(/Instance id must be a non-empty string/);
    });

    it('rejects a whitespace-only instance id', () => {
        expect(() => IdentifierFactory.createServiceAddress('log', '   '))
            .toThrow(/Instance id must be a non-empty string/);
    });
});
