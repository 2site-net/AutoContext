import { describe, it, expect } from 'vitest';
import { parseRules, hashRule } from '../../src/rule-parser';

const singleRuleDoc = `---
description: "Test"
---
# Test

- **Do** always use curly braces for control flow statements.
`;

const multiRuleDoc = `---
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

describe('parseRules', () => {
    it('should parse a single rule', () => {
        const rules = parseRules(singleRuleDoc);

        expect(rules).toHaveLength(1);
        expect(rules[0].text).toBe('- **Do** always use curly braces for control flow statements.');
        expect(rules[0].startLine).toBe(5);
        expect(rules[0].endLine).toBe(5);
        expect(rules[0].hash).toHaveLength(12);
    });

    it('should parse multiple rules', () => {
        const rules = parseRules(multiRuleDoc);

        expect(rules).toHaveLength(3);
        expect(rules[0].text).toContain('write true');
        expect(rules[1].text).toContain('CancellationToken');
        expect(rules[2].text).toContain('async void');
    });

    it('should handle * bullet style', () => {
        const rules = parseRules(starBulletDoc);

        expect(rules).toHaveLength(2);
        expect(rules[0].text).toContain('strict: true');
        expect(rules[1].text).toContain('type assertions');
    });

    it('should parse rules across sections', () => {
        const rules = parseRules(sectionedDoc);

        expect(rules).toHaveLength(3);
        expect(rules[0].text).toContain('leading underscore');
        expect(rules[1].text).toContain('PascalCase');
        expect(rules[2].text).toContain('nest conditional');
    });

    it('should assign unique hashes to distinct rules', () => {
        const rules = parseRules(multiRuleDoc);
        const hashes = rules.map(r => r.hash);

        expect(new Set(hashes).size).toBe(hashes.length);
    });

    it('should produce deterministic hashes', () => {
        const rules1 = parseRules(multiRuleDoc);
        const rules2 = parseRules(multiRuleDoc);

        expect(rules1.map(r => r.hash)).toEqual(rules2.map(r => r.hash));
    });

    it('should return empty array for content with no rules', () => {
        const content = `---
description: "Test"
---
# Just a heading

Some paragraph text.
`;

        expect(parseRules(content)).toHaveLength(0);
    });

    it('should handle CRLF line endings', () => {
        const content = '# Test\r\n\r\n- **Do** use CRLF.\r\n- **Don\'t** mix line endings.\r\n';
        const rules = parseRules(content);

        expect(rules).toHaveLength(2);
        expect(rules[0].text).toContain('use CRLF');
        expect(rules[1].text).toContain('mix line endings');
    });

    it('should handle trailing blank lines at end of file', () => {
        const content = '# Test\n\n- **Do** something.\n\n\n\n';
        const rules = parseRules(content);

        expect(rules).toHaveLength(1);
        expect(rules[0].text).toBe('- **Do** something.');
    });

    it('should stop a rule at a heading', () => {
        const content = `# Section 1

- **Do** rule one.

## Section 2

Some paragraph.
`;
        const rules = parseRules(content);

        expect(rules).toHaveLength(1);
        expect(rules[0].text).toBe('- **Do** rule one.');
    });

    it('should include indented continuation lines in a rule', () => {
        const content = `# Test

- **Do** use curly braces
  for all control flow statements.
- **Don't** nest ternaries.
`;
        const rules = parseRules(content);

        expect(rules).toHaveLength(2);
        expect(rules[0].text).toContain('for all control flow statements');
        expect(rules[1].text).toContain('nest ternaries');
    });
});

describe('hashRule', () => {
    it('should match hash from parseRules for the same text', () => {
        const rules = parseRules(singleRuleDoc);
        const hash = hashRule(rules[0].text);

        expect(hash).toBe(rules[0].hash);
    });

    it('should normalize whitespace differences', () => {
        const hash1 = hashRule('- **Do** use curly braces.');
        const hash2 = hashRule('- **Do**  use  curly  braces.');

        expect(hash1).toBe(hash2);
    });
});
