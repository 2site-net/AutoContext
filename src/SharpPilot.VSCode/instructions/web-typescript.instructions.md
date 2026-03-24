---
description: "Use when generating or editing TypeScript code, working with types, generics, utility types, or TypeScript-specific patterns."
applyTo: "**/*.{ts,tsx,mts,cts}"
---
# TypeScript Guidelines

> These rules cover TypeScript-specific patterns — type safety, generics, utility types, and common anti-patterns. JavaScript guidelines also apply to TypeScript files.

- **Do** enable `strict: true` in `tsconfig.json` — activates `strictNullChecks`, `noImplicitAny`, `strictFunctionTypes`, and several other checks in one flag; do not patch individual strict flags instead.
- **Do** use `unknown` instead of `any` for values whose type is not yet determined — `unknown` forces a type check before use; `any` silently disables type checking for that value and everything it touches.
- **Do** add explicit return type annotations to exported functions and methods — prevents type widening, documents intent, and catches code paths that accidentally return `undefined`.
- **Do** use utility types (`Partial`, `Required`, `Pick`, `Omit`, `Record`, `Readonly`, `ReturnType`, `Parameters`, `Awaited`) to derive types from existing ones rather than writing duplicate type definitions.
- **Do** use discriminated unions with a shared literal discriminant field (e.g., `kind`, `type`, `tag`) to model distinct states — enables exhaustive narrowing in `switch`/`if` chains and prevents impossible state combinations.
- **Do** constrain generic type parameters with `extends` — avoids implicitly `any` type params and gives callers better inference (e.g., `<T extends object>`, `<T extends string | number>`).
- **Do** use `readonly` on properties and `ReadonlyArray<T>` (or `readonly T[]`) for data that should not be mutated after construction — communicates intent and prevents accidental reassignment.
- **Do** use `never` as an exhaustiveness check at the end of `switch` or `if-else` chains over discriminated unions — assigning an unhandled case to `never` causes a compile error when a new union member is added later.
- **Do** use `satisfies` (TS 4.9+) to validate an expression against a type while preserving its most specific inferred literal type — useful for typed config objects where you want both type safety and autocomplete on literal values.
- **Do** prefer `interface` for object shapes intended for extension or declaration merging; use `type` aliases for unions, intersections, mapped types, conditional types, and primitive aliases.
- **Do** use `as const` on constant arrays and objects to preserve literal types — without it, TypeScript widens `'admin'` to `string`, losing the ability to derive precise union types or use the values in type positions.
- **Do** give type guard functions a `value is T` return type annotation instead of `boolean` — without the predicate form, TypeScript does not narrow the type at the call site even when the check is correct.
- **Do** enable `noUncheckedIndexedAccess` in `tsconfig.json` on new projects — it makes array index access (`arr[i]`) and record lookup (`record[key]`) return `T | undefined` instead of `T`, closing the gap between the type system and runtime reality; note it is not included in `strict: true` and is noisy to retrofit onto existing codebases.
- **Don't** use type assertions (`as SomeType`) to silence compiler errors — narrow properly with `typeof`, `instanceof`, `in`, or user-defined type guards; reserve `as` only when you have verified the type through other means and add a comment explaining why.
- **Don't** use `// @ts-ignore` — use `// @ts-expect-error` with a brief explanation instead; it produces a compile error when the suppression is no longer needed, preventing stale suppressions.
- **Don't** use `any` — it disables type checking entirely for that value; use `unknown` for untyped inputs, proper types for known shapes, or generics for parameterized behaviour.
- **Don't** use `enum` — prefer a `const` object with `as const` and a derived `typeof` union for enumerated string values; native enums generate runtime JavaScript, have surprising numeric reverse-mapping, and don't tree-shake cleanly.
- **Don't** use `Function` (capital F), `Object` (capital O), or `{}` as types — they accept too broadly; use specific function signatures, `object` (lowercase), or `Record<string, unknown>` instead.
- **Don't** use non-null assertions (`!`) without proof — only apply them when you've verified by other means that the value cannot be `null` or `undefined`, and add a comment explaining why.
