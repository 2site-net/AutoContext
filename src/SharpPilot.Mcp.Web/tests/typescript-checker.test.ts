import { describe, it, expect } from 'vitest';
import { TypeScriptChecker } from '../src/checkers/typescript/typescript-checker.js';

describe('TypeScriptChecker', () => {
    const checker = new TypeScriptChecker();

    it('should pass clean TypeScript code', () => {
        const source = `
            interface User {
                name: string;
            }
        `;

        expect(checker.check(source)).toBe('✅ All enabled TypeScript checks passed.');
    });

    it('should report failures from sub-checkers', () => {
        const source = `
            const data: any = {};
        `;

        const result = checker.check(source);
        expect(result).toMatch(/^❌/);
    });

    it('should throw on empty input', () => {
        expect(() => checker.check('')).toThrow(Error);
        expect(() => checker.check('   ')).toThrow(Error);
    });
});
