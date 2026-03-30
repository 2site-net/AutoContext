import { describe, it, expect } from 'vitest';
import { TypeScriptCodingStyleChecker } from '../src/checkers/typescript/typescript-coding-style-checker.js';

describe('TypeScriptCodingStyleChecker', () => {
    const checker = new TypeScriptCodingStyleChecker();

    it('should pass clean TypeScript code', () => {
        const source = `
            interface User {
                name: string;
                age: number;
            }

            function greet(user: User): string {
                return \`Hello, \${user.name}\`;
            }
        `;

        expect(checker.check(source)).toMatch(/^✅/);
    });

    it('should flag use of any type', () => {
        const source = `
            function process(data: any): void {
                console.log(data);
            }
        `;

        const result = checker.check(source);
        expect(result).toMatch(/^❌/);
        expect(result).toContain('any');
    });

    it('should flag as any assertion', () => {
        const source = `
            const value = someVariable as any;
        `;

        const result = checker.check(source);
        expect(result).toMatch(/^❌/);
        expect(result).toContain('any');
    });

    it('should flag enum declarations', () => {
        const source = `
            enum Direction {
                Up,
                Down,
                Left,
                Right,
            }
        `;

        const result = checker.check(source);
        expect(result).toMatch(/^❌/);
        expect(result).toContain('enum');
    });

    it('should flag @ts-ignore', () => {
        const source = `
            // @ts-ignore
            const x = 1 + '2';
        `;

        const result = checker.check(source);
        expect(result).toMatch(/^❌/);
        expect(result).toContain('@ts-expect-error');
    });

    it('should flag Function type', () => {
        const source = `
            function execute(callback: Function): void {
                callback();
            }
        `;

        const result = checker.check(source);
        expect(result).toMatch(/^❌/);
        expect(result).toContain('Function');
    });

    it('should flag Object type', () => {
        const source = `
            function process(data: Object): void {
                console.log(data);
            }
        `;

        const result = checker.check(source);
        expect(result).toMatch(/^❌/);
        expect(result).toContain('Object');
    });

    it('should report multiple violations', () => {
        const source = `
            // @ts-ignore
            const x: any = 1;
            enum Status { Active, Inactive }
        `;

        const result = checker.check(source);
        expect(result).toMatch(/^❌/);
        expect(result).toContain('@ts-expect-error');
        expect(result).toContain('any');
        expect(result).toContain('enum');
    });

    it('should not flag any in comments', () => {
        const source = `
            // This can accept any value via unknown
            function process(data: unknown): void {
                console.log(data);
            }
        `;

        expect(checker.check(source)).toMatch(/^✅/);
    });
});
