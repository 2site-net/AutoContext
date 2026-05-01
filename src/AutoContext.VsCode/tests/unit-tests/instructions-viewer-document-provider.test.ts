import { describe, it, expect, vi, beforeEach } from 'vitest';

import { InstructionsViewerDocumentProvider, instructionScheme } from '#src/instructions-viewer-document-provider';
import { AutoContextConfigManager } from '#src/autocontext-config-manager.js';
import { InstructionsFileParser } from '#src/instructions-file-parser';

import { readFile } from 'node:fs/promises';

vi.mock('node:fs/promises', () => ({
    readFile: vi.fn(),
    stat: vi.fn(async () => ({ mtimeMs: 1 })),
}));

import { workspace, Uri } from '#testing/fakes/fake-vscode';
import { createFakeLogger } from '#testing/fakes';
import { testInstructionsContent } from '#testing/fixtures';

const mockLogger = createFakeLogger();

beforeEach(() => {
    vi.clearAllMocks();
    InstructionsFileParser['fileCache'].clear();
    workspace.workspaceFolders = [{ uri: { fsPath: '/workspace' } }];
});

describe('InstructionsViewerDocumentProvider', () => {
    it('should return file content unchanged when no instructions are disabled', async () => {
        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) return '{}';
            return testInstructionsContent;
        });

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const provider = new InstructionsViewerDocumentProvider('/ext', configManager, mockLogger);
        const uri = { scheme: instructionScheme, path: 'test.instructions.md' } as unknown as import('vscode').Uri;
        const result = await provider.provideTextDocumentContent(uri);

        expect.soft(result).toBe(testInstructionsContent);
    });

    it('should insert [DISABLED] tag for disabled instructions', async () => {
        const { instructions: parsedInstructions } = InstructionsFileParser.parse(testInstructionsContent);
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

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const provider = new InstructionsViewerDocumentProvider('/ext', configManager, mockLogger);
        const uri = { scheme: instructionScheme, path: 'test.instructions.md' } as unknown as import('vscode').Uri;
        const result = await provider.provideTextDocumentContent(uri);

        expect(result).toContain('**[DISABLED]** **Do**');
        expect.soft(result).not.toContain("**[DISABLED]** **Don't**");
    });

    it('should build URI with the correct scheme', () => {
        vi.mocked(readFile).mockResolvedValue('{}');

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const provider = new InstructionsViewerDocumentProvider('/ext', configManager, mockLogger);
        provider.buildUri('code-review.instructions.md');

        expect.soft(Uri.from).toHaveBeenCalledWith({
            scheme: instructionScheme,
            path: 'code-review.instructions.md',
        });
    });
});
