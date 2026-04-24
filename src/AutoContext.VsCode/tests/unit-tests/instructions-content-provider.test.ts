import { describe, it, expect, vi, beforeEach } from 'vitest';

import { InstructionsContentProvider, instructionScheme } from '../../src/instructions-content-provider';
import { AutoContextConfigManager } from '../../src/autocontext-config';
import { InstructionsParser } from '../../src/instructions-parser';

import { readFile, stat } from 'node:fs/promises';

vi.mock('node:fs/promises', () => ({
    readFile: vi.fn(),
    stat: vi.fn(async () => ({ mtimeMs: 1 })),
}));

import { workspace, Uri } from './_fakes/fake-vscode';
import { createFakeOutputChannel } from './_fakes';
import { testInstructionsContent } from './_fixtures';

const mockOutputChannel = createFakeOutputChannel();

beforeEach(() => {
    vi.clearAllMocks();
    InstructionsParser['fileCache'].clear();
    workspace.workspaceFolders = [{ uri: { fsPath: '/workspace' } }];
});

describe('InstructionsContentProvider', () => {
    it('should return file content unchanged when no instructions are disabled', async () => {
        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) return '{}';
            return testInstructionsContent;
        });

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockOutputChannel);
        const provider = new InstructionsContentProvider('/ext', configManager, mockOutputChannel);
        const uri = { scheme: instructionScheme, path: 'test.instructions.md' } as unknown as import('vscode').Uri;
        const result = await provider.provideTextDocumentContent(uri);

        expect.soft(result).toBe(testInstructionsContent);
    });

    it('should insert [DISABLED] tag for disabled instructions', async () => {
        const { instructions: parsedInstructions } = InstructionsParser.parse(testInstructionsContent);
        const firstInstructionId = parsedInstructions[0].id;

        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) {
                return JSON.stringify({
                    instructions: {
                        'test.instructions.md': {
                            version: '0.5',
                            'disabledInstructions': [firstInstructionId],
                        },
                    },
                });
            }
            return testInstructionsContent;
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
