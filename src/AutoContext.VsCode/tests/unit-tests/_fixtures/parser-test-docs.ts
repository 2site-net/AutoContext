export const singleInstructionDoc = `---
description: "Test"
---
# Test

- [INST0001] **Do** always use curly braces for control flow statements.
`;

export const multiInstructionDoc = `---
description: "Test"
---
# Async / Await Guidelines

- [INST0001] **Do** write true \`async\`/\`await\` code, don't mix sync and async code.
- [INST0002] **Do** add an optional \`CancellationToken ct = default\` as the final parameter.
- [INST0003] **Don't** use \`async void\` except for event handlers.
`;

export const starBulletDoc = `---
description: "Test"
---
# TypeScript

* [INST0001] **Do** enable \`strict: true\` in \`tsconfig.json\`.
* [INST0002] **Don't** use type assertions (\`as SomeType\`) to silence compiler errors.
`;

export const sectionedDoc = `---
description: "Test"
---
# C# Coding Style

## Naming

- [INST0001] **Do** name private instance fields with a leading underscore.
- [INST0002] **Do** use PascalCase for all constants.

## Language Features

- [INST0003] **Don't** nest conditional expressions.
`;
