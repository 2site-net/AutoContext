import { describe, it, expect } from 'vitest';
import { parseInstructions } from '../../src/instruction-parser';

const singleInstructionDoc = `---
description: "Test"
---
# Test

- [INST0001] **Do** always use curly braces for control flow statements.
`;

const multiInstructionDoc = `---
description: "Test"
---
# Async / Await Guidelines

- [INST0001] **Do** write true \`async\`/\`await\` code, don't mix sync and async code.
- [INST0002] **Do** add an optional \`CancellationToken ct = default\` as the final parameter.
- [INST0003] **Don't** use \`async void\` except for event handlers.
`;

const starBulletDoc = `---
description: "Test"
---
# TypeScript

* [INST0001] **Do** enable \`strict: true\` in \`tsconfig.json\`.
* [INST0002] **Don't** use type assertions (\`as SomeType\`) to silence compiler errors.
`;

const sectionedDoc = `---
description: "Test"
---
# C# Coding Style

## Naming

- [INST0001] **Do** name private instance fields with a leading underscore.
- [INST0002] **Do** use PascalCase for all constants.

## Language Features

- [INST0003] **Don't** nest conditional expressions.
`;

describe('parseInstructions', () => {
    it('should parse a single instruction with ID', () => {
        const { instructions } = parseInstructions(singleInstructionDoc);

        expect(instructions).toHaveLength(1);
        expect(instructions[0].text).toBe('- [INST0001] **Do** always use curly braces for control flow statements.');
        expect(instructions[0].startLine).toBe(5);
        expect(instructions[0].endLine).toBe(5);
        expect(instructions[0].id).toBe('INST0001');
    });

    it('should parse multiple instructions', () => {
        const { instructions } = parseInstructions(multiInstructionDoc);

        expect(instructions).toHaveLength(3);
        expect(instructions[0].text).toContain('write true');
        expect(instructions[1].text).toContain('CancellationToken');
        expect(instructions[2].text).toContain('async void');
    });

    it('should handle * bullet style with IDs', () => {
        const { instructions } = parseInstructions(starBulletDoc);

        expect(instructions).toHaveLength(2);
        expect(instructions[0].id).toBe('INST0001');
        expect(instructions[0].text).toContain('strict: true');
        expect(instructions[1].id).toBe('INST0002');
        expect(instructions[1].text).toContain('type assertions');
    });

    it('should parse instructions across sections', () => {
        const { instructions } = parseInstructions(sectionedDoc);

        expect(instructions).toHaveLength(3);
        expect(instructions[0].text).toContain('leading underscore');
        expect(instructions[1].text).toContain('PascalCase');
        expect(instructions[2].text).toContain('nest conditional');
    });

    it('should assign unique IDs to distinct instructions', () => {
        const { instructions } = parseInstructions(multiInstructionDoc);
        const ids = instructions.map(r => r.id);

        expect(new Set(ids).size).toBe(ids.length);
    });

    it('should produce deterministic IDs', () => {
        const { instructions: first } = parseInstructions(multiInstructionDoc);
        const { instructions: second } = parseInstructions(multiInstructionDoc);

        expect(first.map(r => r.id)).toEqual(second.map(r => r.id));
    });

    it('should return empty array for content with no instructions', () => {
        const content = `---
description: "Test"
---
# Just a heading

Some paragraph text.
`;

        expect(parseInstructions(content).instructions).toHaveLength(0);
    });

    it('should handle CRLF line endings', () => {
        const content = '# Test\r\n\r\n- [INST0001] **Do** use CRLF.\r\n- [INST0002] **Don\'t** mix line endings.\r\n';
        const { instructions } = parseInstructions(content);

        expect(instructions).toHaveLength(2);
        expect(instructions[0].text).toContain('use CRLF');
        expect(instructions[1].text).toContain('mix line endings');
    });

    it('should handle trailing blank lines at end of file', () => {
        const content = '# Test\n\n- [INST0001] **Do** something.\n\n\n\n';
        const { instructions } = parseInstructions(content);

        expect(instructions).toHaveLength(1);
        expect(instructions[0].text).toBe('- [INST0001] **Do** something.');
    });

    it('should stop an instruction at a heading', () => {
        const content = `# Section 1

- [INST0001] **Do** instruction one.

## Section 2

Some paragraph.
`;
        const { instructions } = parseInstructions(content);

        expect(instructions).toHaveLength(1);
        expect(instructions[0].text).toBe('- [INST0001] **Do** instruction one.');
    });

    it('should include indented continuation lines in an instruction', () => {
        const content = `# Test

- [INST0001] **Do** use curly braces
  for all control flow statements.
- [INST0002] **Don't** nest ternaries.
`;
        const { instructions } = parseInstructions(content);

        expect(instructions).toHaveLength(2);
        expect(instructions[0].text).toContain('for all control flow statements');
        expect(instructions[1].text).toContain('nest ternaries');
    });

    it('should set id to undefined for instructions without an ID tag', () => {
        const content = '# Test\n\n- **Do** something without an ID.\n';
        const { instructions } = parseInstructions(content);

        expect(instructions).toHaveLength(1);
        expect(instructions[0].id).toBeUndefined();
    });

    it('should emit missing-id diagnostic for instructions without an ID tag', () => {
        const content = '# Test\n\n- **Do** something without an ID.\n';
        const { diagnostics } = parseInstructions(content);

        expect(diagnostics).toHaveLength(1);
        expect(diagnostics[0].kind).toBe('missing-id');
        expect(diagnostics[0].line).toBe(2);
    });

    it('should emit duplicate-id diagnostic for repeated IDs', () => {
        const content = `# Test

- [INST0001] **Do** first thing.
- [INST0001] **Do** second thing.
`;
        const { diagnostics } = parseInstructions(content);

        const dup = diagnostics.find(d => d.kind === 'duplicate-id');
        expect(dup).toBeDefined();
        expect(dup!.message).toContain('INST0001');
    });

    it('should emit malformed-id diagnostic for invalid ID tags', () => {
        const content = '# Test\n\n- [WRONG] **Do** something.\n';
        const { diagnostics } = parseInstructions(content);

        const malformed = diagnostics.find(d => d.kind === 'malformed-id');
        expect(malformed).toBeDefined();
        expect(malformed!.message).toContain('WRONG');
    });

    it('should emit no diagnostics for well-formed instructions with IDs', () => {
        const { diagnostics } = parseInstructions(multiInstructionDoc);

        expect(diagnostics).toHaveLength(0);
    });

    it('should not emit malformed-id diagnostic for markdown links', () => {
        const content = '# Test\n\n- [Reference](https://example.com) for more info.\n';
        const { diagnostics } = parseInstructions(content);

        expect(diagnostics.filter(d => d.kind === 'malformed-id')).toHaveLength(0);
    });
});
