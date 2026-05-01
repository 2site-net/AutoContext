import { describe, it, expect, vi, beforeEach } from 'vitest';

import { InstructionsViewerCodeLensProvider } from '#src/instructions-viewer-codelens-provider';
import { AutoContextConfigManager } from '#src/autocontext-config-manager.js';
import { instructionScheme } from '#src/instructions-viewer-document-provider';
import { InstructionsFileParser } from '#src/instructions-file-parser';
import { InstructionsFilesManifestLoader } from '#src/instructions-files-manifest-loader';
import { commandIds } from '#src/ui-constants';
import { join } from 'node:path';

import { readFile } from 'node:fs/promises';

vi.mock('node:fs/promises', () => ({
    readFile: vi.fn(),
    stat: vi.fn(async () => ({ mtimeMs: 1 })),
}));

import { workspace } from '#testing/fakes/fake-vscode';
import { createFakeLogger, createFakeDetector } from '#testing/fakes';
import { testInstructionsContent } from '#testing/fixtures';
import { makeDocument } from '#testing/utils';

const mockLogger = createFakeLogger();
const fakeDetector = createFakeDetector();

beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(fakeDetector.get).mockReset();
    InstructionsFileParser['fileCache'].clear();
    workspace.workspaceFolders = [{ uri: { fsPath: '/workspace' } }];
});

describe('InstructionsViewerCodeLensProvider', () => {
    const catalog = new InstructionsFilesManifestLoader(join(__dirname, '..', '..')).load();

    it('should return empty array for non-instruction documents', async () => {
        vi.mocked(readFile).mockResolvedValue('{}');

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const provider = new InstructionsViewerCodeLensProvider({ extensionPath: '/ext', configManager, detector: fakeDetector, manifest: catalog, logger: mockLogger });

        const lenses = await provider.provideCodeLenses(makeDocument('file', 'test.md'));

        expect.soft(lenses).toEqual([]);
    });

    it('should return one CodeLens per instruction when no instructions are disabled', async () => {
        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) return '{}';
            return testInstructionsContent;
        });

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const provider = new InstructionsViewerCodeLensProvider({ extensionPath: '/ext', configManager, detector: fakeDetector, manifest: catalog, logger: mockLogger });

        const lenses = await provider.provideCodeLenses(makeDocument(instructionScheme, 'test.instructions.md'));

        const { instructions: parsedInstructions } = InstructionsFileParser.parse(testInstructionsContent);
        expect(lenses).toHaveLength(parsedInstructions.length);
        expect.soft(lenses.every(lens => {
            const cmd = lens.command as { title: string; command: string };
            return cmd.title.includes('Disable Instruction') && cmd.command === commandIds.ToggleInstruction;
        })).toBe(true);
    });

    it('should show Enable Instruction for disabled instructions', async () => {
        const { instructions: parsedInstructions } = InstructionsFileParser.parse(testInstructionsContent);
        const firstId = parsedInstructions[0].id;

        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) {
                return JSON.stringify({
                    instructions: {
                        'test.instructions.md': {
                            version: '0.5',
                            'disabledInstructions': [firstId],
                        },
                    },
                });
            }
            return testInstructionsContent;
        });

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const provider = new InstructionsViewerCodeLensProvider({ extensionPath: '/ext', configManager, detector: fakeDetector, manifest: catalog, logger: mockLogger });

        const lenses = await provider.provideCodeLenses(makeDocument(instructionScheme, 'test.instructions.md'));

        expect(lenses).toHaveLength(parsedInstructions.length + 1);

        const toggleLenses = lenses.filter(l => (l.command as { command: string }).command === commandIds.ToggleInstruction);
        const enableLens = toggleLenses.find(l => (l.command as { title: string }).title.includes('Enable Instruction'));
        const disableLens = toggleLenses.find(l => (l.command as { title: string }).title.includes('Disable Instruction'));

        expect(enableLens).toBeDefined();
        expect.soft(disableLens).toBeDefined();
    });

    it('should include Reset All Instructions lens when instructions are disabled', async () => {
        const { instructions: parsedInstructions } = InstructionsFileParser.parse(testInstructionsContent);
        const firstId = parsedInstructions[0].id;

        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) {
                return JSON.stringify({
                    instructions: {
                        'test.instructions.md': {
                            version: '0.5',
                            'disabledInstructions': [firstId],
                        },
                    },
                });
            }
            return testInstructionsContent;
        });

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const provider = new InstructionsViewerCodeLensProvider({ extensionPath: '/ext', configManager, detector: fakeDetector, manifest: catalog, logger: mockLogger });

        const lenses = await provider.provideCodeLenses(makeDocument(instructionScheme, 'test.instructions.md'));

        const resetLens = lenses.find(l => (l.command as { command: string }).command === commandIds.ResetInstructions);

        expect.soft(resetLens).toBeDefined();
        expect.soft((resetLens?.command as { title: string })?.title).toContain('Reset All Instructions');
        expect.soft((resetLens?.command as { arguments: string[] })?.arguments).toEqual(['test.instructions.md']);
    });

    it('should not include Reset All Instructions lens when no instructions are disabled', async () => {
        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) return '{}';
            return testInstructionsContent;
        });

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const provider = new InstructionsViewerCodeLensProvider({ extensionPath: '/ext', configManager, detector: fakeDetector, manifest: catalog, logger: mockLogger });

        const lenses = await provider.provideCodeLenses(makeDocument(instructionScheme, 'test.instructions.md'));

        const resetLens = lenses.find(l => (l.command as { command: string }).command === commandIds.ResetInstructions);
        expect.soft(resetLens).toBeUndefined();
    });

    it('should return empty array for overridden instructions', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenContextKeys).mockReturnValue(new Set(['autocontext.instructions.lang-csharp']));
        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) return '{}';
            return testInstructionsContent;
        });

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const provider = new InstructionsViewerCodeLensProvider({ extensionPath: '/ext', configManager, detector: fakeDetector, manifest: catalog, logger: mockLogger });

        const lenses = await provider.provideCodeLenses(makeDocument(instructionScheme, 'lang-csharp.instructions.md'));

        expect.soft(lenses).toEqual([]);
    });

    it('should return empty array for instructions whose context is not detected', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);
        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) return '{}';
            return testInstructionsContent;
        });

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const provider = new InstructionsViewerCodeLensProvider({ extensionPath: '/ext', configManager, detector: fakeDetector, manifest: catalog, logger: mockLogger });

        const lenses = await provider.provideCodeLenses(makeDocument(instructionScheme, 'lang-csharp.instructions.md'));

        expect.soft(lenses).toEqual([]);
    });
});
