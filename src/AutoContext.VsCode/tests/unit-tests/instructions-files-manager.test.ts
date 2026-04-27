import { describe, it, expect, vi, beforeEach } from 'vitest';

import { InstructionsFilesManager } from '../../src/instructions-files-manager';
import { AutoContextConfigManager } from '../../src/autocontext-config';
import { InstructionsFileParser } from '../../src/instructions-file-parser';
import { InstructionsFilesManifestLoader } from '../../src/instructions-files-manifest-loader';
import { join } from 'node:path';

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

import { workspace } from '#testing/fakes/fake-vscode';
import { createFakeLogger } from '#testing/fakes';
import { testInstructionsContent } from '#testing/fixtures';

const mockLogger = createFakeLogger();

beforeEach(() => {
    vi.clearAllMocks();
    InstructionsFileParser['fileCache'].clear();
    workspace.workspaceFolders = [{ uri: { fsPath: '/workspace' } }];
});

describe('InstructionsFilesManager', () => {
    const catalog = new InstructionsFilesManifestLoader(join(__dirname, '..', '..')).load();

    it('should write all instruction files when no instructions are disabled', async () => {
        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) return '{}';
            return testInstructionsContent;
        });

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const writer = new InstructionsFilesManager('/ext', configManager, catalog, mockLogger);
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
            return testInstructionsContent;
        });

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const writer = new InstructionsFilesManager('/ext', configManager, catalog, mockLogger);
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
        const { instructions: parsedInstructions } = InstructionsFileParser.parse(testInstructionsContent);
        const firstId = parsedInstructions[0].id;
        const targetFileName = catalog.instructions[0].name;

        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.autocontext.json')) {
                return JSON.stringify({
                    instructions: {
                        [targetFileName]: {
                            version: '0.5',
                            'disabledInstructions': [firstId],
                        },
                    },
                });
            }
            return testInstructionsContent;
        });

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const writer = new InstructionsFilesManager('/ext', configManager, catalog, mockLogger);
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

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const writer = new InstructionsFilesManager('/ext', configManager, catalog, mockLogger);
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

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const writer = new InstructionsFilesManager('/ext', configManager, catalog, mockLogger);
        await writer.removeOrphanedStagingDirs();

        expect(readdir).not.toHaveBeenCalled();
        expect.soft(rm).not.toHaveBeenCalled();
    });

    it('should remove stale staging dirs older than 1 hour', async () => {
        vi.mocked(access).mockResolvedValue(undefined);
        vi.mocked(readdir).mockResolvedValue(['stale_hash_01'] as unknown as Awaited<ReturnType<typeof readdir>>);
        vi.mocked(stat).mockResolvedValue({ mtimeMs: Date.now() - 2 * 60 * 60 * 1000 } as import('node:fs').Stats);
        vi.mocked(readFile).mockResolvedValue('{}');

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const writer = new InstructionsFilesManager('/ext', configManager, catalog, mockLogger);
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

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const writer = new InstructionsFilesManager('/ext', configManager, catalog, mockLogger);
        await writer.removeOrphanedStagingDirs();

        expect.soft(rm).not.toHaveBeenCalled();
    });

    it('should dispose debounce timer and subscriptions', () => {
        vi.mocked(readFile).mockResolvedValue('{}');

        const configManager = new AutoContextConfigManager('/ext', '0.5.0', mockLogger);
        const writer = new InstructionsFilesManager('/ext', configManager, catalog, mockLogger);

        expect.soft(() => writer.dispose()).not.toThrow();
    });

    it('should log to logger when write fails via workspace folder change', async () => {
        vi.mocked(readFile).mockResolvedValue('{}');

        const failingConfigManager = {
            read: vi.fn().mockRejectedValue(new Error('read boom')),
            readSync: vi.fn(() => ({})),
            onDidChange: vi.fn(() => ({ dispose: vi.fn() })),
        } as unknown as AutoContextConfigManager;

        const oc = createFakeLogger();
        const _writer = new InstructionsFilesManager('/ext', failingConfigManager, catalog, oc);

        // eslint-disable-next-line @typescript-eslint/no-unsafe-member-access
        const wsFolderCallback = (workspace.onDidChangeWorkspaceFolders as any).mock.calls.at(-1)![0] as () => void;
        wsFolderCallback();

        await vi.waitFor(() => {
            expect(oc.error).toHaveBeenCalledWith(
                'Failed to write on workspace change',
                expect.objectContaining({ message: 'read boom' }),
            );
        });

        _writer.dispose();
    });

    it('should log to logger when write fails via config change debounce', async () => {
        vi.useFakeTimers();
        vi.mocked(readFile).mockResolvedValue('{}');

        const failingConfigManager = {
            read: vi.fn().mockRejectedValue(new Error('read boom')),
            readSync: vi.fn(() => ({})),
            onDidChange: vi.fn(() => ({ dispose: vi.fn() })),
        } as unknown as AutoContextConfigManager;

        const oc = createFakeLogger();
        const _writer = new InstructionsFilesManager('/ext', failingConfigManager, catalog, oc);

        const configChangeCallback = vi.mocked(failingConfigManager.onDidChange).mock.calls[0][0] as () => void;
        configChangeCallback();
        await vi.advanceTimersByTimeAsync(250);

        await vi.waitFor(() => {
            expect(oc.error).toHaveBeenCalledWith(
                'Failed to write on config change',
                expect.objectContaining({ message: 'read boom' }),
            );
        });

        vi.useRealTimers();
        _writer.dispose();
    });
});
