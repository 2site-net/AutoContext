import { describe, it, expect, vi, beforeEach } from 'vitest';

import { InstructionDecorationManager } from '../../src/instruction-decoration-manager';
import { SharpPilotConfigManager } from '../../src/sharppilot-config';
import { instructionScheme } from '../../src/instruction-content-provider';
import { parseInstructions } from '../../src/instruction-parser';

import { readFileSync } from 'node:fs';

vi.mock('node:fs', () => ({
    readFileSync: vi.fn(),
    writeFileSync: vi.fn(),
    unlinkSync: vi.fn(),
}));

import { workspace, window as vscodeWindow } from './__mocks__/vscode';

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

function makeEditor(scheme: string, path: string) {
    return {
        document: { uri: { scheme, path } },
        setDecorations: vi.fn(),
    } as unknown as import('vscode').TextEditor;
}

describe('InstructionDecorationManager', () => {
    it('should not set decorations for non-instruction editors', () => {
        vi.mocked(readFileSync).mockReturnValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const manager = new InstructionDecorationManager('/ext', configManager);

        const editor = makeEditor('file', '/some/file.ts');
        manager.applyDecorations(editor);

        expect((editor as unknown as { setDecorations: ReturnType<typeof vi.fn> }).setDecorations).not.toHaveBeenCalled();
    });

    it('should set empty decorations when no instructions are disabled', () => {
        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const manager = new InstructionDecorationManager('/ext', configManager);

        const editor = makeEditor(instructionScheme, 'test.instructions.md');
        manager.applyDecorations(editor);

        expect((editor as unknown as { setDecorations: ReturnType<typeof vi.fn> }).setDecorations).toHaveBeenCalledWith(
            expect.anything(),
            [],
        );
    });

    it('should set decoration ranges for disabled instructions', () => {
        const parsedInstructions = parseInstructions(testContent);
        const firstHash = parsedInstructions[0].hash;

        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) {
                return JSON.stringify({
                    instructions: { disabledInstructions: { 'test.instructions.md': [firstHash] } },
                });
            }
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const manager = new InstructionDecorationManager('/ext', configManager);

        const editor = makeEditor(instructionScheme, 'test.instructions.md');
        manager.applyDecorations(editor);

        const setDecorationsCall = (editor as unknown as { setDecorations: ReturnType<typeof vi.fn> }).setDecorations;
        expect(setDecorationsCall).toHaveBeenCalledWith(
            expect.anything(),
            expect.arrayContaining([expect.anything()]),
        );

        const ranges = setDecorationsCall.mock.calls[0][1];
        expect(ranges).toHaveLength(1);
    });

    it('should refresh all visible editors', () => {
        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return testContent;
        });

        const editor1 = makeEditor(instructionScheme, 'test.instructions.md');
        const editor2 = makeEditor('file', '/some/file.ts');
        vscodeWindow.visibleTextEditors = [editor1, editor2] as unknown[];

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const manager = new InstructionDecorationManager('/ext', configManager);
        manager.refreshAll();

        // Only the instruction editor should get decorations.
        expect((editor1 as unknown as { setDecorations: ReturnType<typeof vi.fn> }).setDecorations).toHaveBeenCalled();
        expect((editor2 as unknown as { setDecorations: ReturnType<typeof vi.fn> }).setDecorations).not.toHaveBeenCalled();
    });
});
