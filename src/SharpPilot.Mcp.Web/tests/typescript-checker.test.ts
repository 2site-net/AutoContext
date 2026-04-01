import { describe, it, expect } from 'vitest';
import { TypeScriptChecker } from '../src/checkers/typescript/typescript-checker.js';

describe('TypeScriptChecker', () => {
    const checker = new TypeScriptChecker();

    it('should pass clean TypeScript code', async () => {
        const source = `
            interface User {
                name: string;
            }
        `;

        expect(await checker.check(source)).toBe('✅ All enabled TypeScript checks passed.');
    });

    it('should report failures from sub-checkers', async () => {
        const source = `
            const data: any = {};
        `;

        const result = await checker.check(source);
        expect(result).toMatch(/^❌/);
    });

    it('should throw on empty input', async () => {
        await expect(checker.check('')).rejects.toThrow(Error);
        await expect(checker.check('   ')).rejects.toThrow(Error);
    });
});
