import { describe, it, expect } from 'vitest';
import { TypeScriptCodingStyleChecker } from '../../../../src/tools/checkers/typescript/typescript-coding-style-checker.js';

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

    it('should not flag any inside string literals', () => {
        const source = `
            const msg = "use any type carefully";
            const tmpl = \`the any keyword\`;
        `;

        expect(checker.check(source)).toMatch(/^✅/);
    });

    it('should flag {} empty type literal', () => {
        const source = `
            function process(data: {}): void {
                console.log(data);
            }
        `;

        const result = checker.check(source);
        expect(result).toMatch(/^❌/);
        expect(result).toContain('{}');
    });

    it('should flag exported function without return type', () => {
        const source = `
            export function add(a: number, b: number) {
                return a + b;
            }
        `;

        const result = checker.check(source);
        expect(result).toMatch(/^❌/);
        expect(result).toContain('return type');
        expect(result).toContain('add');
    });

    it('should not flag non-exported function without return type', () => {
        const source = `
            function add(a: number, b: number) {
                return a + b;
            }
        `;

        expect(checker.check(source)).toMatch(/^✅/);
    });

    it('should not flag exported function with return type', () => {
        const source = `
            export function add(a: number, b: number): number {
                return a + b;
            }
        `;

        expect(checker.check(source)).toMatch(/^✅/);
    });

    it('should flag exported arrow function without return type', () => {
        const source = `
            export const add = (a: number, b: number) => a + b;
        `;

        const result = checker.check(source);
        expect(result).toMatch(/^❌/);
        expect(result).toContain('return type');
        expect(result).toContain('add');
    });

    it('should not flag exported arrow function with return type', () => {
        const source = `
            export const add = (a: number, b: number): number => a + b;
        `;

        expect(checker.check(source)).toMatch(/^✅/);
    });

    it('should flag export default function without return type', () => {
        const source = `
            export default (x: number) => x + 1;
        `;

        const result = checker.check(source);
        expect(result).toMatch(/^❌/);
        expect(result).toContain('Default-exported');
    });

    it('should flag unconstrained generic type parameter', () => {
        const source = `
            function identity<T>(x: T): T {
                return x;
            }
        `;

        const result = checker.check(source);
        expect(result).toMatch(/^❌/);
        expect(result).toContain('extends');
        expect(result).toContain('T');
    });

    it('should not flag constrained generic type parameter', () => {
        const source = `
            function identity<T extends object>(x: T): T {
                return x;
            }
        `;

        expect(checker.check(source)).toMatch(/^✅/);
    });

    it('should flag as type assertion', () => {
        const source = `
            const el = document.getElementById('root') as HTMLDivElement;
        `;

        const result = checker.check(source);
        expect(result).toMatch(/^❌/);
        expect(result).toContain('as');
    });

    it('should not flag as const assertion', () => {
        const source = `
            const colors = ['red', 'green', 'blue'] as const;
        `;

        expect(checker.check(source)).toMatch(/^✅/);
    });

    it('should flag non-null assertion', () => {
        const source = `
            const el = document.getElementById('root')!;
        `;

        const result = checker.check(source);
        expect(result).toMatch(/^❌/);
        expect(result).toContain('non-null');
    });

    it('should flag type alias with object literal body', () => {
        const source = `
            type User = {
                name: string;
                age: number;
            };
        `;

        const result = checker.check(source);
        expect(result).toMatch(/^❌/);
        expect(result).toContain('interface User');
    });

    it('should not flag type alias for union', () => {
        const source = `
            type Status = 'active' | 'inactive';
        `;

        expect(checker.check(source)).toMatch(/^✅/);
    });

    it('should not flag type alias for intersection', () => {
        const source = `
            type WithId = Base & { id: string };
        `;

        expect(checker.check(source)).toMatch(/^✅/);
    });

    it('should not flag type alias for mapped type', () => {
        const source = `
            type ReadonlyUser = { readonly [K in keyof User]: User[K] };
        `;

        expect(checker.check(source)).toMatch(/^✅/);
    });

    it('should skip all checks when disabled and no editorconfig', () => {
        const source = `
            const data: any = {};
            enum Direction { Up, Down }
        `;

        const result = checker.check(source, { __disabled: 'true' });
        expect(result).toMatch(/^✅/);
    });

    it('should skip all checks when disabled even with violations', () => {
        // All current TS checks are INST-only (no EC backing),
        // so disabled mode produces no violations.
        const source = `
            // @ts-ignore
            function process(data: any): void {}
        `;

        const result = checker.check(source, { __disabled: 'true' });
        expect(result).toMatch(/^✅/);
    });
});
