import { describe, it, expect, vi, beforeEach } from 'vitest';

import { RuleCodeLensProvider, toggleRuleCommandId, resetRulesCommandId } from '../../src/rule-codelens-provider';
import { SharpPilotConfigManager } from '../../src/sharppilot-config';
import { instructionScheme } from '../../src/instruction-content-provider';
import { parseRules } from '../../src/rule-parser';

import { readFileSync } from 'node:fs';

vi.mock('node:fs', () => ({
    readFileSync: vi.fn(),
    writeFileSync: vi.fn(),
    unlinkSync: vi.fn(),
}));

import { workspace } from './__mocks__/vscode';

beforeEach(() => {
    vi.clearAllMocks();
    workspace.workspaceFolders = [{ uri: { fsPath: '/workspace' } }];
});

const testContent = `---
description: "Test"
---
# Test

- **Do** always use curly braces.
- **Don't** use async void.
`;

function makeDocument(scheme: string, path: string) {
    return { uri: { scheme, path } } as unknown as import('vscode').TextDocument;
}

describe('RuleCodeLensProvider', () => {
    it('should return empty array for non-instruction documents', () => {
        vi.mocked(readFileSync).mockReturnValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const provider = new RuleCodeLensProvider('/ext', configManager);

        const lenses = provider.provideCodeLenses(makeDocument('file', 'test.md'));

        expect(lenses).toEqual([]);
    });

    it('should return one CodeLens per rule when no rules are disabled', () => {
        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const provider = new RuleCodeLensProvider('/ext', configManager);

        const lenses = provider.provideCodeLenses(makeDocument(instructionScheme, 'test.instructions.md'));

        const rules = parseRules(testContent);
        expect(lenses).toHaveLength(rules.length);

        for (const lens of lenses) {
            const cmd = lens.command as { title: string; command: string };
            expect(cmd.title).toContain('Disable Rule');
            expect(cmd.command).toBe(toggleRuleCommandId);
        }
    });

    it('should show Enable Rule for disabled rules', () => {
        const rules = parseRules(testContent);
        const firstHash = rules[0].hash;

        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) {
                return JSON.stringify({
                    instructions: { disabledRules: { 'test.instructions.md': [firstHash] } },
                });
            }
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const provider = new RuleCodeLensProvider('/ext', configManager);

        const lenses = provider.provideCodeLenses(makeDocument(instructionScheme, 'test.instructions.md'));

        // Should have reset lens + one per rule.
        expect(lenses).toHaveLength(rules.length + 1);

        const toggleLenses = lenses.filter(l => (l.command as { command: string }).command === toggleRuleCommandId);
        const enableLens = toggleLenses.find(l => (l.command as { title: string }).title.includes('Enable Rule'));
        const disableLens = toggleLenses.find(l => (l.command as { title: string }).title.includes('Disable Rule'));

        expect(enableLens).toBeDefined();
        expect(disableLens).toBeDefined();
    });

    it('should include Reset All Rules lens when rules are disabled', () => {
        const rules = parseRules(testContent);
        const firstHash = rules[0].hash;

        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) {
                return JSON.stringify({
                    instructions: { disabledRules: { 'test.instructions.md': [firstHash] } },
                });
            }
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const provider = new RuleCodeLensProvider('/ext', configManager);

        const lenses = provider.provideCodeLenses(makeDocument(instructionScheme, 'test.instructions.md'));

        const resetLens = lenses.find(l => (l.command as { command: string }).command === resetRulesCommandId);
        expect(resetLens).toBeDefined();
        expect((resetLens!.command as { title: string }).title).toContain('Reset All Rules');
        expect((resetLens!.command as { arguments: string[] }).arguments).toEqual(['test.instructions.md']);
    });

    it('should not include Reset All Rules lens when no rules are disabled', () => {
        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const provider = new RuleCodeLensProvider('/ext', configManager);

        const lenses = provider.provideCodeLenses(makeDocument(instructionScheme, 'test.instructions.md'));

        const resetLens = lenses.find(l => (l.command as { command: string }).command === resetRulesCommandId);
        expect(resetLens).toBeUndefined();
    });
});
