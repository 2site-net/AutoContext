import { describe, it, expect, vi, beforeEach } from 'vitest';

import { InstructionsCodeLensProvider, toggleInstructionCommandId, resetInstructionsCommandId } from '../../src/instructions-codelens-provider';
import { SharpPilotConfigManager } from '../../src/sharppilot-config';
import { instructionScheme } from '../../src/instructions-content-provider';
import { InstructionsParser } from '../../src/instructions-parser';

import { readFileSync } from 'node:fs';

vi.mock('node:fs', () => ({
    readFileSync: vi.fn(),
    writeFileSync: vi.fn(),
    unlinkSync: vi.fn(),
}));

import { workspace } from './__mocks__/vscode';

const fakeDetector = {
    get: vi.fn((_key: string) => false),
    onDidDetect: vi.fn(() => ({ dispose: vi.fn() })),
} as unknown as import('../../src/workspace-context-detector').WorkspaceContextDetector;

beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(fakeDetector.get).mockReset();
    workspace.workspaceFolders = [{ uri: { fsPath: '/workspace' } }];
});

const testContent = `---
description: "Test"
---
# Test

- [INST0001] **Do** always use curly braces.
- [INST0002] **Don't** use async void.
`;

function makeDocument(scheme: string, path: string) {
    return { uri: { scheme, path } } as unknown as import('vscode').TextDocument;
}

describe('InstructionsCodeLensProvider', () => {
    it('should return empty array for non-instruction documents', () => {
        vi.mocked(readFileSync).mockReturnValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const provider = new InstructionsCodeLensProvider('/ext', configManager, fakeDetector);

        const lenses = provider.provideCodeLenses(makeDocument('file', 'test.md'));

        expect.soft(lenses).toEqual([]);
    });

    it('should return one CodeLens per instruction when no instructions are disabled', () => {
        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const provider = new InstructionsCodeLensProvider('/ext', configManager, fakeDetector);

        const lenses = provider.provideCodeLenses(makeDocument(instructionScheme, 'test.instructions.md'));

        const { instructions: parsedInstructions } = InstructionsParser.parse(testContent);
        expect(lenses).toHaveLength(parsedInstructions.length);
        expect.soft(lenses.every(lens => {
            const cmd = lens.command as { title: string; command: string };
            return cmd.title.includes('Disable Instruction') && cmd.command === toggleInstructionCommandId;
        })).toBe(true);
    });

    it('should show Enable Instruction for disabled instructions', () => {
        const { instructions: parsedInstructions } = InstructionsParser.parse(testContent);
        const firstId = parsedInstructions[0].id;

        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) {
                return JSON.stringify({
                    instructions: { disabled: { 'test.instructions.md': [firstId] } },
                });
            }
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const provider = new InstructionsCodeLensProvider('/ext', configManager, fakeDetector);

        const lenses = provider.provideCodeLenses(makeDocument(instructionScheme, 'test.instructions.md'));

        expect(lenses).toHaveLength(parsedInstructions.length + 1);

        const toggleLenses = lenses.filter(l => (l.command as { command: string }).command === toggleInstructionCommandId);
        const enableLens = toggleLenses.find(l => (l.command as { title: string }).title.includes('Enable Instruction'));
        const disableLens = toggleLenses.find(l => (l.command as { title: string }).title.includes('Disable Instruction'));

        expect(enableLens).toBeDefined();
        expect.soft(disableLens).toBeDefined();
    });

    it('should include Reset All Instructions lens when instructions are disabled', () => {
        const { instructions: parsedInstructions } = InstructionsParser.parse(testContent);
        const firstId = parsedInstructions[0].id;

        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) {
                return JSON.stringify({
                    instructions: { disabled: { 'test.instructions.md': [firstId] } },
                });
            }
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const provider = new InstructionsCodeLensProvider('/ext', configManager, fakeDetector);

        const lenses = provider.provideCodeLenses(makeDocument(instructionScheme, 'test.instructions.md'));

        const resetLens = lenses.find(l => (l.command as { command: string }).command === resetInstructionsCommandId);

        expect.soft(resetLens).toBeDefined();
        expect.soft((resetLens?.command as { title: string })?.title).toContain('Reset All Instructions');
        expect.soft((resetLens?.command as { arguments: string[] })?.arguments).toEqual(['test.instructions.md']);
    });

    it('should not include Reset All Instructions lens when no instructions are disabled', () => {
        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const provider = new InstructionsCodeLensProvider('/ext', configManager, fakeDetector);

        const lenses = provider.provideCodeLenses(makeDocument(instructionScheme, 'test.instructions.md'));

        const resetLens = lenses.find(l => (l.command as { command: string }).command === resetInstructionsCommandId);
        expect.soft(resetLens).toBeUndefined();
    });

    it('should return empty array for instructions whose context is not detected', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);
        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const provider = new InstructionsCodeLensProvider('/ext', configManager, fakeDetector);

        const lenses = provider.provideCodeLenses(makeDocument(instructionScheme, 'lang-csharp.instructions.md'));

        expect.soft(lenses).toEqual([]);
    });
});
