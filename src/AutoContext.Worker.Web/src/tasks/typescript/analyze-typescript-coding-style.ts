import ts from 'typescript';
import type { McpTask } from '../../hosting/mcp-task.js';

interface Violation {
    readonly line: number;
    readonly message: string;
}

function lineOf(sourceFile: ts.SourceFile, pos: number): number {
    return sourceFile.getLineAndCharacterOfPosition(pos).line + 1;
}

function hasExportModifier(node: ts.Node): boolean {
    if (!ts.canHaveModifiers(node)) return false;
    const modifiers = ts.getModifiers(node);
    return modifiers?.some(m => m.kind === ts.SyntaxKind.ExportKeyword) ?? false;
}

function checkTsIgnoreComments(sourceFile: ts.SourceFile, violations: Violation[]): void {
    const text = sourceFile.getFullText();
    const pattern = /\/\/\s*@ts-ignore\b/g;
    let match: RegExpExecArray | null;
    while ((match = pattern.exec(text)) !== null) {
        violations.push({
            line: lineOf(sourceFile, match.index),
            message: 'Use `// @ts-expect-error` instead of `// @ts-ignore` — it errors when the suppression is no longer needed.',
        });
    }
}

function findViolations(content: string): readonly Violation[] {
    const sourceFile = ts.createSourceFile(
        'input.ts',
        content,
        ts.ScriptTarget.Latest,
        true,
        ts.ScriptKind.TS,
    );

    const violations: Violation[] = [];

    // [typescript INST0004]: @ts-ignore in comments (regex — comments are not AST nodes)
    checkTsIgnoreComments(sourceFile, violations);

    function visit(node: ts.Node): void {
        // [typescript INST0011]: enum declarations
        if (ts.isEnumDeclaration(node)) {
            violations.push({
                line: lineOf(sourceFile, node.getStart(sourceFile)),
                message: 'Prefer a `const` object with `as const` and a derived union type instead of `enum`.',
            });
        }

        // [typescript INST0005]: any type keyword
        if (node.kind === ts.SyntaxKind.AnyKeyword) {
            violations.push({
                line: lineOf(sourceFile, node.getStart(sourceFile)),
                message: 'Avoid `any` — use `unknown` for untyped inputs, proper types for known shapes, or generics for parameterized behavior.',
            });
        }

        // [typescript INST0012]: Function / Object type references
        if (ts.isTypeReferenceNode(node)) {
            const name = node.typeName.getText(sourceFile);
            if (name === 'Function') {
                violations.push({
                    line: lineOf(sourceFile, node.getStart(sourceFile)),
                    message: 'Avoid `Function` as a type — use a specific function signature instead.',
                });
            } else if (name === 'Object') {
                violations.push({
                    line: lineOf(sourceFile, node.getStart(sourceFile)),
                    message: 'Avoid `Object` as a type — use `object`, `Record<string, unknown>`, or a specific interface.',
                });
            }
        }

        // [typescript INST0012]: {} empty type literal
        if (ts.isTypeLiteralNode(node) && node.members.length === 0) {
            violations.push({
                line: lineOf(sourceFile, node.getStart(sourceFile)),
                message: 'Avoid `{}` as a type — it matches any non-nullish value; use `object` or `Record<string, unknown>` instead.',
            });
        }

        // [typescript INST0010]: type alias with plain object literal → prefer interface
        if (
            ts.isTypeAliasDeclaration(node) &&
            ts.isTypeLiteralNode(node.type) &&
            node.type.members.length > 0
        ) {
            const name = node.name.getText(sourceFile);
            violations.push({
                line: lineOf(sourceFile, node.getStart(sourceFile)),
                message: `Use \`interface ${name}\` instead of \`type ${name} = { ... }\` for object shapes.`,
            });
        }

        // [typescript INST0006]: exported function without return type
        if (
            ts.isFunctionDeclaration(node) &&
            hasExportModifier(node) &&
            !node.type
        ) {
            const name = node.name?.getText(sourceFile) ?? '<anonymous>';
            violations.push({
                line: lineOf(sourceFile, node.getStart(sourceFile)),
                message: `Exported function \`${name}\` should have an explicit return type annotation.`,
            });
        }

        // [typescript INST0006]: exported arrow / function-expression initializers
        if (ts.isVariableStatement(node) && hasExportModifier(node)) {
            for (const decl of node.declarationList.declarations) {
                const init = decl.initializer;
                if (init && (ts.isArrowFunction(init) || ts.isFunctionExpression(init)) && !init.type) {
                    const name = decl.name.getText(sourceFile);
                    violations.push({
                        line: lineOf(sourceFile, decl.getStart(sourceFile)),
                        message: `Exported function \`${name}\` should have an explicit return type annotation.`,
                    });
                }
            }
        }

        // [typescript INST0006]: export default arrow / function expression
        if (ts.isExportAssignment(node) && !node.isExportEquals) {
            const expr = node.expression;
            if ((ts.isArrowFunction(expr) || ts.isFunctionExpression(expr)) && !expr.type) {
                violations.push({
                    line: lineOf(sourceFile, node.getStart(sourceFile)),
                    message: 'Default-exported function should have an explicit return type annotation.',
                });
            }
        }

        // [typescript INST0009]: unconstrained generic type parameters
        if (ts.isTypeParameterDeclaration(node) && !node.constraint) {
            const name = node.name.getText(sourceFile);
            violations.push({
                line: lineOf(sourceFile, node.getStart(sourceFile)),
                message: `Generic type parameter \`${name}\` should be constrained with \`extends\`.`,
            });
        }

        // [typescript INST0018]: as type assertions (skip as const)
        if (ts.isAsExpression(node) && node.type.getText(sourceFile) !== 'const') {
            violations.push({
                line: lineOf(sourceFile, node.getStart(sourceFile)),
                message: 'Avoid type assertions (`as`) — narrow with `typeof`, `instanceof`, `in`, or type guards instead.',
            });
        }

        // [typescript INST0019]: non-null assertions (!)
        if (ts.isNonNullExpression(node)) {
            violations.push({
                line: lineOf(sourceFile, node.getStart(sourceFile)),
                message: 'Avoid non-null assertions (`!`) — verify nullability with a proper check or type guard.',
            });
        }

        ts.forEachChild(node, visit);
    }

    visit(sourceFile);
    violations.sort((a, b) => a.line - b.line);
    return violations;
}

function formatReport(violations: readonly Violation[]): string {
    const lines = violations.map(v => `  Line ${v.line}: ${v.message}`);
    return `❌ TypeScript Coding Style\n${lines.join('\n')}`;
}

/**
 * `analyze_typescript_coding_style` — enforces coding-style rules from
 * `lang-typescript.instructions.md`: no `any`, no enums, no `@ts-ignore`,
 * no `as` type assertions, no non-null assertions, no unconstrained
 * generics, no `Function`/`Object`/`{}` types, prefer `interface` over
 * `type` for object shapes, and explicit return types on exported
 * functions.
 *
 * Request `data`:   `{ "content": "<typescript-source>" }`
 * Response `output`: `{ "passed": <bool>, "report": "<text>" }`
 */
export class AnalyzeTypeScriptCodingStyleTask implements McpTask {
    readonly taskName = 'analyze_typescript_coding_style';

    async execute(
        data: Record<string, unknown>,
        signal: AbortSignal,
    ): Promise<unknown> {
        signal.throwIfAborted();

        const content = data['content'];
        if (typeof content !== 'string') {
            throw new Error("'data.content' is required and must be a string.");
        }
        if (content.trim().length === 0) {
            throw new Error("'data.content' must not be empty or whitespace.");
        }

        const violations = findViolations(content);
        signal.throwIfAborted();
        const passed = violations.length === 0;
        const report = passed
            ? '✅ TypeScript Coding Style'
            : formatReport(violations);

        return { passed, report };
    }
}
