import { describe, it, expect, vi, beforeEach } from 'vitest';

import { InstructionsDecorationManager } from '../../src/instructions-decoration-manager';
import { AutoContextConfigManager } from '../../src/autocontext-config';
import { instructionScheme } from '../../src/instructions-content-provider';
import { InstructionsParser } from '../../src/instructions-parser';

import { readFile, stat } from 'node:fs/promises';

vi.mock('node:fs/promises', () => ({
    readFile: vi.fn(),
    stat: vi.fn(async () => ({ mtimeMs: 1 })),
}));

import { workspace, window as vscodeWindow } from './_fakes/fake-vscode';
import { createFakeOutputChannel } from './_fakes';
import { testInstructionsContent } from './_fixtures';
import { makeEditor } from './_utils';

const mockOutputChannel = createFakeOutputChannel();

beforeEach(() => {
    vi.clearAllMocks();
    InstructionsParser['fileCache'].clear();
    workspace.workspaceFolders = [{ uri: { fsPath: '/workspace' } }];
});

describe('InstructionsDecorationManager', () => {
    it('should not set decorations for non-instruction editors', async () => {
        vi.mocked(readFile).mockResolvedValue('{}');

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockOutputChannel);
        const manager = new InstructionsDecorationManager('/ext', configManager, mockOutputChannel);

        const editor = makeEditor('file', '/some/file.ts');
        await manager.applyDecorations(editor);

        expect.soft((editor as unknown as { setDecorations: ReturnType<typeof vi.fn> }).setDecorations).not.toHaveBeenCalled();
    });

    it('should set empty decorations when no instructions are disabled', async () => {
        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) return '{}';
            return testInstructionsContent;
        });

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockOutputChannel);
        const manager = new InstructionsDecorationManager('/ext', configManager, mockOutputChannel);

        const editor = makeEditor(instructionScheme, 'test.instructions.md');
        await manager.applyDecorations(editor);

        expect.soft((editor as unknown as { setDecorations: ReturnType<typeof vi.fn> }).setDecorations).toHaveBeenCalledWith(
            expect.anything(),
            [],
        );
    });

    it('should set decoration ranges for disabled instructions', async () => {
        const { instructions: parsedInstructions } = InstructionsParser.parse(testInstructionsContent);
        const firstId = parsedInstructions[0].id;

        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) {
                return JSON.stringify({
                    instructions: {
                        'test.instructions.md': {
                            version: '0.5',
                            'disabled-instructions': [firstId],
                        },
                    },
                });
            }
            return testInstructionsContent;
        });

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockOutputChannel);
        const manager = new InstructionsDecorationManager('/ext', configManager, mockOutputChannel);

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
            return testInstructionsContent;
        });

        const editor1 = makeEditor(instructionScheme, 'test.instructions.md');
        const editor2 = makeEditor('file', '/some/file.ts');
        vscodeWindow.visibleTextEditors = [editor1, editor2] as unknown[];

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockOutputChannel);
        const manager = new InstructionsDecorationManager('/ext', configManager, mockOutputChannel);
        manager.refreshAll();

        // refreshAll fires void (fire-and-forget) async applyDecorations calls;
        // flush the microtask queue so they settle before asserting.
        await new Promise(resolve => setTimeout(resolve, 0));

        expect((editor1 as unknown as { setDecorations: ReturnType<typeof vi.fn> }).setDecorations).toHaveBeenCalled();
        expect.soft((editor2 as unknown as { setDecorations: ReturnType<typeof vi.fn> }).setDecorations).not.toHaveBeenCalled();
    });

    it('should log to outputChannel when applyDecorations fails in onDidChangeActiveTextEditor', async () => {
        vi.mocked(readFile).mockResolvedValue(testInstructionsContent);

        const failingConfigManager = {
            getDisabledInstructions: vi.fn().mockRejectedValue(new Error('config boom')),
            onDidChange: vi.fn(() => ({ dispose: vi.fn() })),
        } as unknown as AutoContextConfigManager;

        const oc = createFakeOutputChannel();
        const _manager = new InstructionsDecorationManager('/ext', failingConfigManager, oc);

        const editorCallback = vi.mocked(vscodeWindow.onDidChangeActiveTextEditor).mock.calls.at(-1)![0] as (e: unknown) => void;
        editorCallback(makeEditor(instructionScheme, 'test.instructions.md'));

        await vi.waitFor(() => {
            expect(oc.appendLine).toHaveBeenCalledWith(
                expect.stringContaining('[Decorations] Failed to apply decorations: config boom'),
            );
        });
    });

    it('should log to outputChannel when applyDecorations fails in refreshAll', async () => {
        vi.mocked(readFile).mockResolvedValue(testInstructionsContent);

        const failingConfigManager = {
            getDisabledInstructions: vi.fn().mockRejectedValue(new Error('config boom')),
            onDidChange: vi.fn(() => ({ dispose: vi.fn() })),
        } as unknown as AutoContextConfigManager;

        const oc = createFakeOutputChannel();
        const manager = new InstructionsDecorationManager('/ext', failingConfigManager, oc);

        const editor = makeEditor(instructionScheme, 'test.instructions.md');
        vscodeWindow.visibleTextEditors = [editor] as unknown[];
        manager.refreshAll();

        await vi.waitFor(() => {
            expect(oc.appendLine).toHaveBeenCalledWith(
                expect.stringContaining('[Decorations] Failed to apply decorations: config boom'),
            );
        });
    });
});
