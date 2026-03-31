import type { Checker } from '../checker.js';

interface Violation {
    readonly line: number;
    readonly message: string;
}

function findViolations(content: string): readonly Violation[] {
    const lines = content.split('\n');
    const violations: Violation[] = [];

    for (let i = 0; i < lines.length; i++) {
        const line = lines[i]!;
        const lineNum = i + 1;
        const trimmed = line.trim();

        // Skip comment and string lines (crude heuristic to reduce false positives)
        if (trimmed.startsWith('//') && !trimmed.startsWith('// @ts-ignore')) {
            continue;
        }

        // INST0004: @ts-ignore should be @ts-expect-error
        if (/\/\/\s*@ts-ignore\b/.test(line)) {
            violations.push({
                line: lineNum,
                message: 'Use `// @ts-expect-error` instead of `// @ts-ignore` — it errors when the suppression is no longer needed.',
            });
        }

        // INST0011: enum declarations
        if (/\benum\s+\w+/.test(trimmed) && !trimmed.startsWith('*') && !trimmed.startsWith('//')) {
            violations.push({
                line: lineNum,
                message: 'Prefer a `const` object with `as const` and a derived union type instead of `enum`.',
            });
        }

        // INST0005: explicit `any` type annotations
        if (/:\s*any\b/.test(line) || /\bas\s+any\b/.test(line) || /<any\b/.test(line)) {
            if (!trimmed.startsWith('//') && !trimmed.startsWith('*')) {
                violations.push({
                    line: lineNum,
                    message: 'Avoid `any` — use `unknown` for untyped inputs, proper types for known shapes, or generics for parameterized behavior.',
                });
            }
        }

        // INST0012: capital-F Function, capital-O Object, or {} as types
        if (/:\s*Function\b/.test(line) && !trimmed.startsWith('//') && !trimmed.startsWith('*')) {
            violations.push({
                line: lineNum,
                message: 'Avoid `Function` as a type — use a specific function signature instead.',
            });
        }
        if (/:\s*Object\b/.test(line) && !trimmed.startsWith('//') && !trimmed.startsWith('*')) {
            violations.push({
                line: lineNum,
                message: 'Avoid `Object` as a type — use `object`, `Record<string, unknown>`, or a specific interface.',
            });
        }
    }

    return violations;
}

function formatReport(violations: readonly Violation[]): string {
    const lines = violations.map(v => `  Line ${v.line}: ${v.message}`);
    return `❌ TypeScript Coding Style\n${lines.join('\n')}`;
}

export class TypeScriptCodingStyleChecker implements Checker {
    readonly toolName = 'check_typescript_coding_style';

    check(content: string, _data?: Record<string, string>): string {
        const violations = findViolations(content);

        if (violations.length === 0) {
            return '✅ TypeScript Coding Style';
        }

        return formatReport(violations);
    }
}
