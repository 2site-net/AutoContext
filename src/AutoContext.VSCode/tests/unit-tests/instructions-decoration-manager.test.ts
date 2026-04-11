import { describe, it, expect, vi, beforeEach } from 'vitest';

import { InstructionsDecorationManager } from '../../src/instructions-decoration-manager';
import { SharpPilotConfigManager } from '../../src/sharppilot-config';
import { instructionScheme } from '../../src/instructions-content-provider';
import { InstructionsParser } from '../../src/instructions-parser';

import { readFile, stat } from 'node:fs/promises';

vi.mock('node:fs/promises', () => ({
    readFile: vi.fn(),
    stat: vi.fn(async () => ({ mtimeMs: 1 })),
}));

import { workspace, window as vscodeWindow } from './__mocks__/vscode';

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

function makeEditor(scheme: string, path: string) {
    return {
        document: { uri: { scheme, path } },
        setDecorations: vi.fn(),
    } as unknown as import('vscode').TextEditor;
}

describe('InstructionsDecorationManager', () => {
    it('should not set decorations for non-instruction editors', async () => {
        vi.mocked(readFile).mockResolvedValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const manager = new InstructionsDecorationManager('/ext', configManager);

        const editor = makeEditor('file', '/some/file.ts');
        await manager.applyDecorations(editor);

        expect.soft((editor as unknown as { setDecorations: ReturnType<typeof vi.fn> }).setDecorations).not.toHaveBeenCalled();
    });

    it('should set empty decorations when no instructions are disabled', async () => {
        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) return '{}';
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const manager = new InstructionsDecorationManager('/ext', configManager);

        const editor = makeEditor(instructionScheme, 'test.instructions.md');
        await manager.applyDecorations(editor);

        expect.soft((editor as unknown as { setDecorations: ReturnType<typeof vi.fn> }).setDecorations).toHaveBeenCalledWith(
            expect.anything(),
            [],
        );
    });

    it('should set decoration ranges for disabled instructions', async () => {
        const { instructions: parsedInstructions } = InstructionsParser.parse(testContent);
        const firstId = parsedInstructions[0].id;

        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) {
                return JSON.stringify({
                    instructions: { disabled: { 'test.instructions.md': [firstId] } },
                });
            }
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const manager = new InstructionsDecorationManager('/ext', configManager);

        const editor = makeEditor(instructionScheme, 'test.instructions.md');
        await manager.applyDecorations(editor);

        const setDecorationsCall = (editor as unknown as { setDecorations: ReturnType<typeof vi.fn> }).setDecorations;
        expect(setDecorationsCall).toHaveBeenCalledWith(
            expect.anything(),
            expect.arrayContaining([expect.anything()]),
        );

        const ranges = setDecorationsCall.mock.calls[0][1];
        expect.soft(ranges).toHaveLength(1);
    });

    it('should refresh all visible editors', async () => {
        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) return '{}';
            return testContent;
        });

        const editor1 = makeEditor(instructionScheme, 'test.instructions.md');
        const editor2 = makeEditor('file', '/some/file.ts');
        vscodeWindow.visibleTextEditors = [editor1, editor2] as unknown[];

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const manager = new InstructionsDecorationManager('/ext', configManager);
        manager.refreshAll();

        // refreshAll fires void (fire-and-forget) async applyDecorations calls;
        // flush the microtask queue so they settle before asserting.
        await new Promise(resolve => setTimeout(resolve, 0));

        expect((editor1 as unknown as { setDecorations: ReturnType<typeof vi.fn> }).setDecorations).toHaveBeenCalled();
        expect.soft((editor2 as unknown as { setDecorations: ReturnType<typeof vi.fn> }).setDecorations).not.toHaveBeenCalled();
    });
});
