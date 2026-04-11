import { describe, it, expect, vi, beforeEach } from 'vitest';

import { InstructionsConfigWriter } from '../../src/instructions-config-writer';
import { SharpPilotConfigManager } from '../../src/sharppilot-config';
import { InstructionsParser } from '../../src/instructions-parser';
import { InstructionsCatalog } from '../../src/instructions-catalog';
import { instructionsFiles } from '../../src/ui-constants';

import { writeFile, readFile, readdir, stat, rm, access } from 'node:fs/promises';

vi.mock('node:fs/promises', () => ({
    readFile: vi.fn(async () => ''),
    writeFile: vi.fn(async () => undefined),
    mkdir: vi.fn(async () => undefined),
    readdir: vi.fn(async () => []),
    stat: vi.fn(async () => ({ mtimeMs: 1 })),
    rm: vi.fn(async () => undefined),
    access: vi.fn(async () => undefined),
}));

import { workspace } from './__mocks__/vscode';

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

describe('InstructionsConfigWriter', () => {
    const catalog = new InstructionsCatalog(instructionsFiles);

    it('should write all instruction files when no instructions are disabled', async () => {
        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) return '{}';
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionsConfigWriter('/ext', configManager, catalog);
        await writer.write();

        expect(writeFile).toHaveBeenCalled();
        const writeCalls = vi.mocked(writeFile).mock.calls;
        const stagingWrites = writeCalls.filter(([path]) =>
            String(path).includes('.workspaces'),
        );
        expect.soft(stagingWrites.length).toBe(catalog.count);
    });

    it('should strip instruction IDs from output', async () => {
        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) return '{}';
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionsConfigWriter('/ext', configManager, catalog);
        await writer.write();

        const writeCalls = vi.mocked(writeFile).mock.calls;
        const stagingWrite = writeCalls.find(([path]) =>
            String(path).includes('.workspaces'),
        );

        expect(stagingWrite).toBeDefined();
        const writtenContent = stagingWrite![1] as string;

        expect.soft(writtenContent).not.toContain('[INST0001]');
        expect.soft(writtenContent).not.toContain('[INST0002]');
        expect.soft(writtenContent).toContain('always use curly braces');
        expect.soft(writtenContent).toContain('async void');
    });

    it('should write filtered content with disabled instructions removed', async () => {
        const { instructions: parsedInstructions } = InstructionsParser.parse(testContent);
        const firstId = parsedInstructions[0].id;
        const targetFileName = catalog.all[0].fileName;

        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) {
                return JSON.stringify({
                    instructions: { disabled: { [targetFileName]: [firstId] } },
                });
            }
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionsConfigWriter('/ext', configManager, catalog);
        await writer.write();

        const writeCalls = vi.mocked(writeFile).mock.calls;
        const targetWrite = writeCalls.find(([path]) =>
            String(path).includes(targetFileName) && String(path).includes('.workspaces'),
        );

        expect(targetWrite).toBeDefined();
        const writtenContent = targetWrite![1] as string;

        expect.soft(writtenContent).not.toContain('always use curly braces');
        expect.soft(writtenContent).toContain('async void');
        expect.soft(writtenContent).not.toContain('[INST0002]');
    });

    it('should preserve non-instruction content unchanged', async () => {
        const contentWithProse = `---
description: "Test"
---
# Guidelines

Some introductory text here.

- [INST0001] **Do** always use curly braces.

## Section Two

More prose below.
`;

        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) return '{}';
            return contentWithProse;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionsConfigWriter('/ext', configManager, catalog);
        await writer.write();

        const writeCalls = vi.mocked(writeFile).mock.calls;
        const stagingWrite = writeCalls.find(([path]) =>
            String(path).includes('.workspaces'),
        );

        expect(stagingWrite).toBeDefined();
        const writtenContent = stagingWrite![1] as string;

        expect.soft(writtenContent).toContain('# Guidelines');
        expect.soft(writtenContent).toContain('Some introductory text here.');
        expect.soft(writtenContent).toContain('## Section Two');
        expect.soft(writtenContent).toContain('More prose below.');
    });

    it('should skip orphan cleanup when .workspaces does not exist', async () => {
        vi.mocked(access).mockRejectedValue(new Error('ENOENT'));
        vi.mocked(readFile).mockResolvedValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionsConfigWriter('/ext', configManager, catalog);
        await writer.removeOrphanedStagingDirs();

        expect(readdir).not.toHaveBeenCalled();
        expect.soft(rm).not.toHaveBeenCalled();
    });

    it('should remove stale staging dirs older than 1 hour', async () => {
        vi.mocked(access).mockResolvedValue(undefined);
        vi.mocked(readdir).mockResolvedValue(['stale_hash_01'] as unknown as Awaited<ReturnType<typeof readdir>>);
        vi.mocked(stat).mockResolvedValue({ mtimeMs: Date.now() - 2 * 60 * 60 * 1000 } as import('node:fs').Stats);
        vi.mocked(readFile).mockResolvedValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionsConfigWriter('/ext', configManager, catalog);
        await writer.removeOrphanedStagingDirs();

        expect.soft(rm).toHaveBeenCalledWith(
            expect.stringContaining('stale_hash_01'),
            { recursive: true },
        );
    });

    it('should not remove staging dirs modified within the last hour', async () => {
        vi.mocked(access).mockResolvedValue(undefined);
        vi.mocked(readdir).mockResolvedValue(['recent_hash_'] as unknown as Awaited<ReturnType<typeof readdir>>);
        vi.mocked(stat).mockResolvedValue({ mtimeMs: Date.now() - 30 * 60 * 1000 } as import('node:fs').Stats);
        vi.mocked(readFile).mockResolvedValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionsConfigWriter('/ext', configManager, catalog);
        await writer.removeOrphanedStagingDirs();

        expect.soft(rm).not.toHaveBeenCalled();
    });

    it('should dispose debounce timer and subscriptions', () => {
        vi.mocked(readFile).mockResolvedValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionsConfigWriter('/ext', configManager, catalog);

        expect.soft(() => writer.dispose()).not.toThrow();
    });
});
