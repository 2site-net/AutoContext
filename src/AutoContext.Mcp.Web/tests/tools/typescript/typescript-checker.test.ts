import { describe, it, expect } from 'vitest';
import { TypeScriptChecker } from '../../../src/tools/typescript/typescript-checker.js';
import { McpToolsClient } from '../../../src/features/mcp-tools/mcp-tools-client.js';
import { NullLogger } from '../../../src/features/logging/logger.js';

describe('TypeScriptChecker', () => {
    const checker = new TypeScriptChecker(
        new McpToolsClient(),
        NullLogger,
    );

    it('should pass clean TypeScript code', async () => {
        const source = `
            interface User {
                name: string;
            }
        `;

        expect.soft(await checker.check(source)).toBe('✅ All enabled TypeScript checks passed.');
    });

    it('should report failures from sub-checkers', async () => {
        const source = `
            const data: any = {};
        `;

        const result = await checker.check(source);

        expect.soft(result).toMatch(/^❌/);
    });

    it('should throw on empty input', async () => {
        await expect(checker.check('')).rejects.toThrow(Error);
        await expect.soft(checker.check('   ')).rejects.toThrow(Error);
    });
});
