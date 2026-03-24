import { describe, it, expect, vi, beforeEach } from 'vitest';

import { InstructionContentProvider, instructionScheme } from '../../src/instruction-content-provider';
import { SharpPilotConfigManager } from '../../src/sharppilot-config';
import { parseInstructions } from '../../src/instruction-parser';

import { readFileSync } from 'node:fs';

vi.mock('node:fs', () => ({
    readFileSync: vi.fn(),
    writeFileSync: vi.fn(),
    unlinkSync: vi.fn(),
}));

import { workspace, Uri } from './__mocks__/vscode';

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

describe('InstructionContentProvider', () => {
    it('should return file content unchanged when no instructions are disabled', () => {
        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const provider = new InstructionContentProvider('/ext', configManager);
        const uri = { scheme: instructionScheme, path: 'test.instructions.md' } as unknown as import('vscode').Uri;
        const result = provider.provideTextDocumentContent(uri);

        expect(result).toBe(testContent);
    });

    it('should insert [DISABLED] tag for disabled instructions', () => {
        const parsedInstructions = parseInstructions(testContent);
        const firstInstructionHash = parsedInstructions[0].hash;

        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) {
                return JSON.stringify({ instructions: { disabledInstructions: { 'test.instructions.md': [firstInstructionHash] } } });
            }
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const provider = new InstructionContentProvider('/ext', configManager);
        const uri = { scheme: instructionScheme, path: 'test.instructions.md' } as unknown as import('vscode').Uri;
        const result = provider.provideTextDocumentContent(uri);

        expect(result).toContain('**[DISABLED]** **Do**');
        expect(result).not.toContain("**[DISABLED]** **Don't**");
    });

    it('should build URI with the correct scheme', () => {
        vi.mocked(readFileSync).mockReturnValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const provider = new InstructionContentProvider('/ext', configManager);
        provider.buildUri('copilot.instructions.md');

        expect(Uri.from).toHaveBeenCalledWith({
            scheme: instructionScheme,
            path: 'copilot.instructions.md',
        });
    });
});
