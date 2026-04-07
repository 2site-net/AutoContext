import { describe, it, expect, vi, beforeEach } from 'vitest';

import { InstructionsConfigWriter } from '../../src/instructions-config-writer';
import { SharpPilotConfigManager } from '../../src/sharppilot-config';
import { InstructionsParser } from '../../src/instructions-parser';
import { InstructionsCatalog } from '../../src/instructions-catalog';
import { instructionEntries } from '../../src/ui-constants';

import { readFileSync, writeFileSync, existsSync, readdirSync, rmSync, statSync } from 'node:fs';

vi.mock('node:fs', () => ({
    readFileSync: vi.fn(),
    writeFileSync: vi.fn(),
    mkdirSync: vi.fn(),
    existsSync: vi.fn(() => false),
    readdirSync: vi.fn(() => []),
    rmSync: vi.fn(),
    statSync: vi.fn(),
}));

import { workspace } from './__mocks__/vscode';

beforeEach(() => {
    vi.clearAllMocks();
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
    const catalog = new InstructionsCatalog(instructionEntries);

    it('should write all instruction files when no instructions are disabled', () => {
        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionsConfigWriter('/ext', configManager, catalog);
        writer.write();

        expect(writeFileSync).toHaveBeenCalled();
        const writeCalls = vi.mocked(writeFileSync).mock.calls;
        const stagingWrites = writeCalls.filter(([path]) =>
            String(path).includes('.workspaces'),
        );
        expect.soft(stagingWrites.length).toBe(catalog.count);
    });

    it('should strip instruction IDs from output', () => {
        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionsConfigWriter('/ext', configManager, catalog);
        writer.write();

        const writeCalls = vi.mocked(writeFileSync).mock.calls;
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

    it('should write filtered content with disabled instructions removed', () => {
        const { instructions: parsedInstructions } = InstructionsParser.parse(testContent);
        const firstId = parsedInstructions[0].id;
        const targetFileName = catalog.all[0].fileName;

        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) {
                return JSON.stringify({
                    instructions: { disabled: { [targetFileName]: [firstId] } },
                });
            }
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionsConfigWriter('/ext', configManager, catalog);
        writer.write();

        const writeCalls = vi.mocked(writeFileSync).mock.calls;
        const targetWrite = writeCalls.find(([path]) =>
            String(path).includes(targetFileName) && String(path).includes('.workspaces'),
        );

        expect(targetWrite).toBeDefined();
        const writtenContent = targetWrite![1] as string;

        expect.soft(writtenContent).not.toContain('always use curly braces');
        expect.soft(writtenContent).toContain('async void');
        expect.soft(writtenContent).not.toContain('[INST0002]');
    });

    it('should preserve non-instruction content unchanged', () => {
        const contentWithProse = `---
description: "Test"
---
# Guidelines

Some introductory text here.

- [INST0001] **Do** always use curly braces.

## Section Two

More prose below.
`;

        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return contentWithProse;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionsConfigWriter('/ext', configManager, catalog);
        writer.write();

        const writeCalls = vi.mocked(writeFileSync).mock.calls;
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

    it('should skip orphan cleanup when .workspaces does not exist', () => {
        vi.mocked(existsSync).mockReturnValue(false);
        vi.mocked(readFileSync).mockReturnValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionsConfigWriter('/ext', configManager, catalog);
        writer.removeOrphanedStagingDirs();

        expect(readdirSync).not.toHaveBeenCalled();
        expect.soft(rmSync).not.toHaveBeenCalled();
    });

    it('should remove stale staging dirs older than 1 hour', () => {
        vi.mocked(existsSync).mockReturnValue(true);
        vi.mocked(readdirSync).mockReturnValue(['stale_hash_01'] as unknown as ReturnType<typeof readdirSync>);
        vi.mocked(statSync).mockReturnValue({ mtimeMs: Date.now() - 2 * 60 * 60 * 1000 } as import('node:fs').Stats);
        vi.mocked(readFileSync).mockReturnValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionsConfigWriter('/ext', configManager, catalog);
        writer.removeOrphanedStagingDirs();

        expect.soft(rmSync).toHaveBeenCalledWith(
            expect.stringContaining('stale_hash_01'),
            { recursive: true },
        );
    });

    it('should not remove staging dirs modified within the last hour', () => {
        vi.mocked(existsSync).mockReturnValue(true);
        vi.mocked(readdirSync).mockReturnValue(['recent_hash_'] as unknown as ReturnType<typeof readdirSync>);
        vi.mocked(statSync).mockReturnValue({ mtimeMs: Date.now() - 30 * 60 * 1000 } as import('node:fs').Stats);
        vi.mocked(readFileSync).mockReturnValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionsConfigWriter('/ext', configManager, catalog);
        writer.removeOrphanedStagingDirs();

        expect.soft(rmSync).not.toHaveBeenCalled();
    });

    it('should dispose debounce timer and subscriptions', () => {
        vi.mocked(readFileSync).mockReturnValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionsConfigWriter('/ext', configManager, catalog);

        expect.soft(() => writer.dispose()).not.toThrow();
    });
});
