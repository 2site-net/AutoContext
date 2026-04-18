import { describe, it, expect, vi, beforeEach } from 'vitest';
import { InstructionsParser } from '../../src/instructions-parser';
import { singleInstructionDoc, multiInstructionDoc, starBulletDoc, sectionedDoc } from './_fixtures';

import { readFile, stat } from 'node:fs/promises';

vi.mock('node:fs/promises', () => ({
    readFile: vi.fn(),
    stat: vi.fn(),
}));

beforeEach(() => {
    vi.clearAllMocks();
    // Clear the static file cache between tests to avoid cross-test pollution.
    InstructionsParser['fileCache'].clear();
});

describe('parseInstructions', () => {
    it('should parse a single instruction with ID', () => {
        const { instructions } = InstructionsParser.parse(singleInstructionDoc);

        expect.soft(instructions).toHaveLength(1);
        expect.soft(instructions[0]?.text).toBe('- [INST0001] **Do** always use curly braces for control flow statements.');
        expect.soft(instructions[0]?.startLine).toBe(5);
        expect.soft(instructions[0]?.endLine).toBe(5);
        expect.soft(instructions[0]?.id).toBe('INST0001');
    });

    it('should extract frontmatter description and version from name', () => {
        const content = `---
description: "My instruction file"
name: "my-instruction (v1.2.3)"
---
# Test

- [INST0001] **Do** something.
`;
        const { frontmatter } = InstructionsParser.parse(content);

        expect.soft(frontmatter.description).toBe('My instruction file');
        expect.soft(frontmatter.version).toBe('1.2.3');
    });

    it('should extract frontmatter with applyTo field', () => {
        const content = `---
description: "Scoped instructions"
applyTo: "**/*.cs"
name: "scoped (v1.0.0)"
---
# Test

- [INST0001] **Do** something.
`;
        const { frontmatter } = InstructionsParser.parse(content);

        expect.soft(frontmatter.description).toBe('Scoped instructions');
        expect.soft(frontmatter.version).toBe('1.0.0');
    });

    it('should return empty frontmatter when no frontmatter block exists', () => {
        const content = `# No frontmatter

- [INST0001] **Do** something.
`;
        const { frontmatter } = InstructionsParser.parse(content);

        expect.soft(frontmatter.description).toBeUndefined();
        expect.soft(frontmatter.version).toBeUndefined();
    });

    it('should return partial frontmatter when only description is present', () => {
        const content = `---
description: "Only description"
---
# Test

- [INST0001] **Do** something.
`;
        const { frontmatter } = InstructionsParser.parse(content);

        expect.soft(frontmatter.description).toBe('Only description');
        expect.soft(frontmatter.version).toBeUndefined();
    });

    it('should parse multiple instructions', () => {
        const { instructions } = InstructionsParser.parse(multiInstructionDoc);

        expect.soft(instructions).toHaveLength(3);
        expect.soft(instructions[0]?.text).toContain('write true');
        expect.soft(instructions[1]?.text).toContain('CancellationToken');
        expect.soft(instructions[2]?.text).toContain('async void');
    });

    it('should extract description from existing fixtures', () => {
        const { frontmatter } = InstructionsParser.parse(singleInstructionDoc);

        expect.soft(frontmatter.description).toBe('Test');
        expect.soft(frontmatter.version).toBeUndefined();
    });

    it('should handle * bullet style with IDs', () => {
        const { instructions } = InstructionsParser.parse(starBulletDoc);

        expect.soft(instructions).toHaveLength(2);
        expect.soft(instructions[0]?.id).toBe('INST0001');
        expect.soft(instructions[0]?.text).toContain('strict: true');
        expect.soft(instructions[1]?.id).toBe('INST0002');
        expect.soft(instructions[1]?.text).toContain('type assertions');
    });

    it('should parse instructions across sections', () => {
        const { instructions } = InstructionsParser.parse(sectionedDoc);

        expect.soft(instructions).toHaveLength(3);
        expect.soft(instructions[0]?.text).toContain('leading underscore');
        expect.soft(instructions[1]?.text).toContain('PascalCase');
        expect.soft(instructions[2]?.text).toContain('nest conditional');
    });

    it('should assign unique IDs to distinct instructions', () => {
        const { instructions } = InstructionsParser.parse(multiInstructionDoc);
        const ids = instructions.map(r => r.id);

        expect.soft(new Set(ids).size).toBe(ids.length);
    });

    it('should produce deterministic IDs', () => {
        const { instructions: first } = InstructionsParser.parse(multiInstructionDoc);
        const { instructions: second } = InstructionsParser.parse(multiInstructionDoc);

        expect.soft(first.map(r => r.id)).toEqual(second.map(r => r.id));
    });

    it('should return empty array for content with no instructions', () => {
        const content = `---
description: "Test"
---
# Just a heading

Some paragraph text.
`;

        expect.soft(InstructionsParser.parse(content).instructions).toHaveLength(0);
    });

    it('should handle CRLF line endings', () => {
        const content = '# Test\r\n\r\n- [INST0001] **Do** use CRLF.\r\n- [INST0002] **Don\'t** mix line endings.\r\n';
        const { instructions } = InstructionsParser.parse(content);

        expect.soft(instructions).toHaveLength(2);
        expect.soft(instructions[0]?.text).toContain('use CRLF');
        expect.soft(instructions[1]?.text).toContain('mix line endings');
    });

    it('should handle trailing blank lines at end of file', () => {
        const content = '# Test\n\n- [INST0001] **Do** something.\n\n\n\n';
        const { instructions } = InstructionsParser.parse(content);

        expect(instructions).toHaveLength(1);
        expect.soft(instructions[0].text).toBe('- [INST0001] **Do** something.');
    });

    it('should stop an instruction at a heading', () => {
        const content = `# Section 1

- [INST0001] **Do** instruction one.

## Section 2

Some paragraph.
`;
        const { instructions } = InstructionsParser.parse(content);

        expect(instructions).toHaveLength(1);
        expect.soft(instructions[0].text).toBe('- [INST0001] **Do** instruction one.');
    });

    it('should include indented continuation lines in an instruction', () => {
        const content = `# Test

- [INST0001] **Do** use curly braces
  for all control flow statements.
- [INST0002] **Don't** nest ternaries.
`;
        const { instructions } = InstructionsParser.parse(content);

        expect.soft(instructions).toHaveLength(2);
        expect.soft(instructions[0]?.text).toContain('for all control flow statements');
        expect.soft(instructions[1]?.text).toContain('nest ternaries');
    });

    it('should set id to undefined for instructions without an ID tag', () => {
        const content = '# Test\n\n- **Do** something without an ID.\n';
        const { instructions } = InstructionsParser.parse(content);

        expect(instructions).toHaveLength(1);
        expect.soft(instructions[0].id).toBeUndefined();
    });

    it('should emit missing-id diagnostic for instructions without an ID tag', () => {
        const content = '# Test\n\n- **Do** something without an ID.\n';
        const { diagnostics } = InstructionsParser.parse(content);

        expect.soft(diagnostics).toHaveLength(1);
        expect.soft(diagnostics[0]?.kind).toBe('missing-id');
        expect.soft(diagnostics[0]?.line).toBe(2);
    });

    it('should emit duplicate-id diagnostic for repeated IDs', () => {
        const content = `# Test

- [INST0001] **Do** first thing.
- [INST0001] **Do** second thing.
`;
        const { diagnostics } = InstructionsParser.parse(content);

        const dup = diagnostics.find(d => d.kind === 'duplicate-id');
        expect(dup).toBeDefined();
        expect.soft(dup!.message).toContain('INST0001');
    });

    it('should emit malformed-id diagnostic for invalid ID tags', () => {
        const content = '# Test\n\n- [WRONG] **Do** something.\n';
        const { diagnostics } = InstructionsParser.parse(content);

        const malformed = diagnostics.find(d => d.kind === 'malformed-id');
        expect(malformed).toBeDefined();
        expect.soft(malformed!.message).toContain('WRONG');
    });

    it('should emit no diagnostics for well-formed instructions with IDs', () => {
        const { diagnostics } = InstructionsParser.parse(multiInstructionDoc);

        expect.soft(diagnostics).toHaveLength(0);
    });

    it('should not emit malformed-id diagnostic for markdown links', () => {
        const content = '# Test\n\n- [Reference](https://example.com) for more info.\n';
        const { diagnostics } = InstructionsParser.parse(content);

        expect.soft(diagnostics.filter(d => d.kind === 'malformed-id')).toHaveLength(0);
    });
});

describe('fromFile', () => {
    it('should read and parse a file', async () => {
        vi.mocked(stat).mockResolvedValue({ mtimeMs: 1000 } as Awaited<ReturnType<typeof stat>>);
        vi.mocked(readFile).mockResolvedValue(singleInstructionDoc);

        const { content, result } = await InstructionsParser.fromFile('/ext/instructions/test.md');

        expect.soft(content).toBe(singleInstructionDoc);
        expect.soft(result.instructions).toHaveLength(1);
        expect.soft(result.instructions[0]?.id).toBe('INST0001');
    });

    it('should return cached result for unchanged file', async () => {
        vi.mocked(stat).mockResolvedValue({ mtimeMs: 1000 } as Awaited<ReturnType<typeof stat>>);
        vi.mocked(readFile).mockResolvedValue(singleInstructionDoc);

        await InstructionsParser.fromFile('/ext/instructions/test.md');
        await InstructionsParser.fromFile('/ext/instructions/test.md');

        expect.soft(readFile).toHaveBeenCalledTimes(1);
    });

    it('should re-parse when file mtime changes', async () => {
        vi.mocked(readFile).mockResolvedValue(singleInstructionDoc);

        vi.mocked(stat).mockResolvedValue({ mtimeMs: 1000 } as Awaited<ReturnType<typeof stat>>);
        await InstructionsParser.fromFile('/ext/instructions/test.md');

        vi.mocked(stat).mockResolvedValue({ mtimeMs: 2000 } as Awaited<ReturnType<typeof stat>>);
        await InstructionsParser.fromFile('/ext/instructions/test.md');

        expect.soft(readFile).toHaveBeenCalledTimes(2);
    });
});
