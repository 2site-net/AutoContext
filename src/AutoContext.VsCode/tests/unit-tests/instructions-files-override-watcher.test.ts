import { describe, it, expect, vi, beforeEach } from 'vitest';
import { workspace, commands } from '#support/fake-vscode';
import { InstructionsFilesOverrideWatcher } from '#src/instructions-files-override-watcher';
import { createFakeLogger } from '#support/fake-logger';
import { fakeUri } from '#support/fake-uri';
import { stubFindFiles } from '#support/fake-workspace-stubs';

const mockLogger = createFakeLogger();

const bundledNames = new Set([
    'lang-csharp.instructions.md',
    'dotnet-coding-standards.instructions.md',
]);

beforeEach(() => {
    vi.clearAllMocks();
    (workspace.findFiles as ReturnType<typeof vi.fn>).mockImplementation(async () => []);
    (workspace.fs.readFile as ReturnType<typeof vi.fn>).mockImplementation(async () => new Uint8Array());
});

describe('InstructionsFilesOverrideWatcher', () => {
    describe('watch (initial scan)', () => {
        it('should start with no overrides and empty maps', async () => {
            const w = new InstructionsFilesOverrideWatcher(bundledNames, mockLogger);
            await w.watch();

            expect.soft(w.getOverriddenFileNames().size).toBe(0);
            expect.soft(w.isOverridden('lang-csharp.instructions.md')).toBe(false);
            expect.soft(w.getOverrideVersion('lang-csharp.instructions.md')).toBeUndefined();
        });

        it('should populate overridden file names for matching bundled instructions', async () => {
            stubFindFiles({
                '.github/instructions/*.instructions.md': [
                    '/.github/instructions/dotnet-coding-standards.instructions.md',
                ],
            });

            const w = new InstructionsFilesOverrideWatcher(bundledNames, mockLogger);
            await w.watch();

            expect(w.isOverridden('dotnet-coding-standards.instructions.md')).toBe(true);
        });

        it('should ignore override files that are not in the bundled set', async () => {
            stubFindFiles({
                '.github/instructions/*.instructions.md': [
                    '/.github/instructions/unknown.instructions.md',
                ],
            });

            const w = new InstructionsFilesOverrideWatcher(bundledNames, mockLogger);
            await w.watch();

            expect(w.getOverriddenFileNames().size).toBe(0);
        });

        it('should clear overrides between scans', async () => {
            stubFindFiles({
                '.github/instructions/*.instructions.md': [
                    '/.github/instructions/dotnet-coding-standards.instructions.md',
                ],
            });
            const w = new InstructionsFilesOverrideWatcher(bundledNames, mockLogger);
            await w.watch();
            expect(w.getOverriddenFileNames().size).toBe(1);

            stubFindFiles({});
            await w.watch();

            expect(w.getOverriddenFileNames().size).toBe(0);
        });

        it('should parse frontmatter version when override file is read', async () => {
            stubFindFiles({
                '.github/instructions/*.instructions.md': [
                    '/.github/instructions/dotnet-coding-standards.instructions.md',
                ],
            });
            const content = '---\nname: "dotnet-coding-standards (v2.5.0)"\n---\nbody\n';
            (workspace.fs.readFile as ReturnType<typeof vi.fn>).mockResolvedValue(
                new TextEncoder().encode(content),
            );

            const w = new InstructionsFilesOverrideWatcher(bundledNames, mockLogger);
            await w.watch();

            expect(w.getOverrideVersion('dotnet-coding-standards.instructions.md')).toBe('2.5.0');
        });

        it('should register override context keys with autocontext.override. prefix', async () => {
            stubFindFiles({
                '.github/instructions/*.instructions.md': [
                    '/.github/instructions/dotnet-coding-standards.instructions.md',
                ],
            });

            const w = new InstructionsFilesOverrideWatcher(bundledNames, mockLogger);
            await w.watch();

            expect.soft(commands.executeCommand).toHaveBeenCalledWith(
                'setContext', 'autocontext.override.dotnet-coding-standards', true,
            );
            expect.soft(commands.executeCommand).toHaveBeenCalledWith(
                'setContext', 'autocontext.override.lang-csharp', false,
            );
        });
    });

    describe('file-system events', () => {
        beforeEach(() => vi.useFakeTimers());

        type WatcherMock = {
            onDidCreate: ReturnType<typeof vi.fn>;
            onDidChange: ReturnType<typeof vi.fn>;
            onDidDelete: ReturnType<typeof vi.fn>;
        };

        it('should rescan when override watcher fires', async () => {
            const w = new InstructionsFilesOverrideWatcher(bundledNames, mockLogger);
            await w.watch();
            expect(w.getOverriddenFileNames().size).toBe(0);

            const watcher = (workspace.createFileSystemWatcher as ReturnType<typeof vi.fn>)
                .mock.results[0].value as WatcherMock;
            const listener = watcher.onDidCreate.mock.calls[0]?.[0] as ((uri: unknown) => void) | undefined;

            stubFindFiles({
                '.github/instructions/*.instructions.md': [
                    '/.github/instructions/dotnet-coding-standards.instructions.md',
                ],
            });
            listener?.(fakeUri('/.github/instructions/dotnet-coding-standards.instructions.md'));
            await vi.advanceTimersByTimeAsync(500);

            expect(w.isOverridden('dotnet-coding-standards.instructions.md')).toBe(true);

            vi.useRealTimers();
        });
    });

    describe('resilience', () => {
        it('should still update state when setContext rejects', async () => {
            stubFindFiles({
                '.github/instructions/*.instructions.md': [
                    '/.github/instructions/dotnet-coding-standards.instructions.md',
                ],
            });
            (commands.executeCommand as ReturnType<typeof vi.fn>).mockRejectedValue(
                new Error('setContext failure'),
            );

            const w = new InstructionsFilesOverrideWatcher(bundledNames, mockLogger);
            await w.watch();

            expect(w.isOverridden('dotnet-coding-standards.instructions.md')).toBe(true);
        });
    });
});
