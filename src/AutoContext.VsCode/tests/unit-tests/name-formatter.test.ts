import { describe, it, expect } from 'vitest';
import { NameFormatter } from '#src/name-formatter';

describe('NameFormatter.toShortName', () => {
    it('should strip the AutoContext. package prefix', () => {
        expect(NameFormatter.toShortName('AutoContext.Worker.DotNet')).toBe('Worker.DotNet');
    });

    it('should pass through names without the prefix unchanged', () => {
        expect(NameFormatter.toShortName('Worker.DotNet')).toBe('Worker.DotNet');
    });

    it('should not match a prefix that only differs in case', () => {
        expect(NameFormatter.toShortName('autocontext.Worker.DotNet')).toBe('autocontext.Worker.DotNet');
    });
});

describe('NameFormatter.toDisplayName', () => {
    it('should swap the package prefix for the display prefix', () => {
        expect(NameFormatter.toDisplayName('AutoContext.Worker.DotNet')).toBe('AutoContext: Worker.DotNet');
    });

    it('should add the display prefix when the input has no package prefix', () => {
        expect(NameFormatter.toDisplayName('Worker.DotNet')).toBe('AutoContext: Worker.DotNet');
    });
});
