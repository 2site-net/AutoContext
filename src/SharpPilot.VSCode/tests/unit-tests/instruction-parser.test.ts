import { describe, it, expect } from 'vitest';
import { parseInstructions, hashInstruction } from '../../src/instruction-parser';

const singleInstructionDoc = `---
description: "Test"
---
# Test

- **Do** always use curly braces for control flow statements.
`;

const multiInstructionDoc = `---
description: "Test"
---
# Async / Await Guidelines

- **Do** write true \`async\`/\`await\` code, don't mix sync and async code.
- **Do** add an optional \`CancellationToken ct = default\` as the final parameter.
- **Don't** use \`async void\` except for event handlers.
`;

const starBulletDoc = `---
description: "Test"
---
# TypeScript

* **Do** enable \`strict: true\` in \`tsconfig.json\`.
* **Don't** use type assertions (\`as SomeType\`) to silence compiler errors.
`;

const sectionedDoc = `---
description: "Test"
---
# C# Coding Style

## Naming

- **Do** name private instance fields with a leading underscore.
- **Do** use PascalCase for all constants.

## Language Features

- **Don't** nest conditional expressions.
`;

describe('parseInstructions', () => {
    it('should parse a single instruction', () => {
        const instructions = parseInstructions(singleInstructionDoc);

        expect(instructions).toHaveLength(1);
        expect(instructions[0].text).toBe('- **Do** always use curly braces for control flow statements.');
        expect(instructions[0].startLine).toBe(5);
        expect(instructions[0].endLine).toBe(5);
        expect(instructions[0].hash).toHaveLength(12);
    });

    it('should parse multiple instructions', () => {
        const instructions = parseInstructions(multiInstructionDoc);

        expect(instructions).toHaveLength(3);
        expect(instructions[0].text).toContain('write true');
        expect(instructions[1].text).toContain('CancellationToken');
        expect(instructions[2].text).toContain('async void');
    });

    it('should handle * bullet style', () => {
        const instructions = parseInstructions(starBulletDoc);

        expect(instructions).toHaveLength(2);
        expect(instructions[0].text).toContain('strict: true');
        expect(instructions[1].text).toContain('type assertions');
    });

    it('should parse instructions across sections', () => {
        const instructions = parseInstructions(sectionedDoc);

        expect(instructions).toHaveLength(3);
        expect(instructions[0].text).toContain('leading underscore');
        expect(instructions[1].text).toContain('PascalCase');
        expect(instructions[2].text).toContain('nest conditional');
    });

    it('should assign unique hashes to distinct instructions', () => {
        const instructions = parseInstructions(multiInstructionDoc);
        const hashes = instructions.map(r => r.hash);

        expect(new Set(hashes).size).toBe(hashes.length);
    });

    it('should produce deterministic hashes', () => {
        const instructions1 = parseInstructions(multiInstructionDoc);
        const instructions2 = parseInstructions(multiInstructionDoc);

        expect(instructions1.map(r => r.hash)).toEqual(instructions2.map(r => r.hash));
    });

    it('should return empty array for content with no instructions', () => {
        const content = `---
description: "Test"
---
# Just a heading

Some paragraph text.
`;

        expect(parseInstructions(content)).toHaveLength(0);
    });

    it('should handle CRLF line endings', () => {
        const content = '# Test\r\n\r\n- **Do** use CRLF.\r\n- **Don\'t** mix line endings.\r\n';
        const instructions = parseInstructions(content);

        expect(instructions).toHaveLength(2);
        expect(instructions[0].text).toContain('use CRLF');
        expect(instructions[1].text).toContain('mix line endings');
    });

    it('should handle trailing blank lines at end of file', () => {
        const content = '# Test\n\n- **Do** something.\n\n\n\n';
        const instructions = parseInstructions(content);

        expect(instructions).toHaveLength(1);
        expect(instructions[0].text).toBe('- **Do** something.');
    });

    it('should stop an instruction at a heading', () => {
        const content = `# Section 1

- **Do** instruction one.

## Section 2

Some paragraph.
`;
        const instructions = parseInstructions(content);

        expect(instructions).toHaveLength(1);
        expect(instructions[0].text).toBe('- **Do** instruction one.');
    });

    it('should include indented continuation lines in an instruction', () => {
        const content = `# Test

- **Do** use curly braces
  for all control flow statements.
- **Don't** nest ternaries.
`;
        const instructions = parseInstructions(content);

        expect(instructions).toHaveLength(2);
        expect(instructions[0].text).toContain('for all control flow statements');
        expect(instructions[1].text).toContain('nest ternaries');
    });
});

describe('hashInstruction', () => {
    it('should match hash from parseInstructions for the same text', () => {
        const instructions = parseInstructions(singleInstructionDoc);
        const hash = hashInstruction(instructions[0].text);

        expect(hash).toBe(instructions[0].hash);
    });

    it('should normalize whitespace differences', () => {
        const hash1 = hashInstruction('- **Do** use curly braces.');
        const hash2 = hashInstruction('- **Do**  use  curly  braces.');

        expect(hash1).toBe(hash2);
    });
});
