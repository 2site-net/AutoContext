import { describe, it, expect, vi, beforeEach } from 'vitest';

import { InstructionsContentProvider, instructionScheme } from '../../src/instructions-content-provider';
import { AutoContextConfigManager } from '../../src/autocontext-config';
import { InstructionsParser } from '../../src/instructions-parser';

import { readFile, stat } from 'node:fs/promises';

vi.mock('node:fs/promises', () => ({
    readFile: vi.fn(),
    stat: vi.fn(async () => ({ mtimeMs: 1 })),
}));

import { workspace, Uri } from './__mocks__/vscode';

const mockOutputChannel = { appendLine: vi.fn() } as unknown as import('vscode').OutputChannel;

beforeEach(() => {
    vi.clearAllMocks();
    InstructionsParser['fileCache'].clear();
    workspace.workspaceFolders = [{ uri: { fsPath: '/workspace' } }];
});

const testContent = `---
description: "Test"
---
# Test

- [INST0001] **Do** always use curly braces.
- [INST0002] **Don't** use async void.
`;

describe('InstructionsContentProvider', () => {
    it('should return file content unchanged when no instructions are disabled', async () => {
        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) return '{}';
            return testContent;
        });

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockOutputChannel);
        const provider = new InstructionsContentProvider('/ext', configManager, mockOutputChannel);
        const uri = { scheme: instructionScheme, path: 'test.instructions.md' } as unknown as import('vscode').Uri;
        const result = await provider.provideTextDocumentContent(uri);

        expect.soft(result).toBe(testContent);
    });

    it('should insert [DISABLED] tag for disabled instructions', async () => {
        const { instructions: parsedInstructions } = InstructionsParser.parse(testContent);
        const firstInstructionId = parsedInstructions[0].id;

        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) {
                return JSON.stringify({ instructions: { disabled: { 'test.instructions.md': [firstInstructionId] } } });
            }
            return testContent;
        });

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockOutputChannel);
        const provider = new InstructionsContentProvider('/ext', configManager, mockOutputChannel);
        const uri = { scheme: instructionScheme, path: 'test.instructions.md' } as unknown as import('vscode').Uri;
        const result = await provider.provideTextDocumentContent(uri);

        expect(result).toContain('**[DISABLED]** **Do**');
        expect.soft(result).not.toContain("**[DISABLED]** **Don't**");
    });

    it('should build URI with the correct scheme', () => {
        vi.mocked(readFile).mockResolvedValue('{}');

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockOutputChannel);
        const provider = new InstructionsContentProvider('/ext', configManager, mockOutputChannel);
        provider.buildUri('code-review.instructions.md');

        expect.soft(Uri.from).toHaveBeenCalledWith({
            scheme: instructionScheme,
            path: 'code-review.instructions.md',
        });
    });
});
