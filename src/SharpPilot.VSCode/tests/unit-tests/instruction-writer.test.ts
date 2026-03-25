import { describe, it, expect, vi, beforeEach } from 'vitest';

import { InstructionWriter } from '../../src/instruction-writer';
import { SharpPilotConfigManager } from '../../src/sharppilot-config';
import { parseInstructions } from '../../src/instruction-parser';
import { instructions } from '../../src/config';

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

describe('InstructionWriter', () => {
    it('should write all instruction files when no instructions are disabled', () => {
        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionWriter('/ext', configManager);
        writer.write();

        // Should write every instruction file to staging + promote to .generated.
        expect(writeFileSync).toHaveBeenCalled();
        const writeCalls = vi.mocked(writeFileSync).mock.calls;
        const stagingWrites = writeCalls.filter(([path]) =>
            String(path).includes('.workspaces'),
        );
        expect(stagingWrites.length).toBe(instructions.length);
    });

    it('should strip instruction IDs from output', () => {
        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionWriter('/ext', configManager);
        writer.write();

        const writeCalls = vi.mocked(writeFileSync).mock.calls;
        const stagingWrite = writeCalls.find(([path]) =>
            String(path).includes('.workspaces'),
        );

        expect(stagingWrite).toBeDefined();
        const writtenContent = stagingWrite![1] as string;

        expect(writtenContent).not.toContain('[INST0001]');
        expect(writtenContent).not.toContain('[INST0002]');
        // Content itself should still be present.
        expect(writtenContent).toContain('always use curly braces');
        expect(writtenContent).toContain('async void');
    });

    it('should write filtered content with disabled instructions removed', () => {
        const { instructions: parsedInstructions } = parseInstructions(testContent);
        const firstId = parsedInstructions[0].id;
        const targetFileName = instructions[0].fileName;

        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) {
                return JSON.stringify({
                    instructions: { disabledInstructions: { [targetFileName]: [firstId] } },
                });
            }
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionWriter('/ext', configManager);
        writer.write();

        // Find the write call for the target instruction file in staging.
        const writeCalls = vi.mocked(writeFileSync).mock.calls;
        const targetWrite = writeCalls.find(([path]) =>
            String(path).includes(targetFileName) && String(path).includes('.workspaces'),
        );

        expect(targetWrite).toBeDefined();
        const writtenContent = targetWrite![1] as string;

        // The disabled instruction's text should not appear.
        expect(writtenContent).not.toContain('always use curly braces');
        // The other instruction should still be present.
        expect(writtenContent).toContain('async void');
        // Tags should be stripped from remaining lines.
        expect(writtenContent).not.toContain('[INST0002]');
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
        const writer = new InstructionWriter('/ext', configManager);
        writer.write();

        const writeCalls = vi.mocked(writeFileSync).mock.calls;
        const stagingWrite = writeCalls.find(([path]) =>
            String(path).includes('.workspaces'),
        );

        expect(stagingWrite).toBeDefined();
        const writtenContent = stagingWrite![1] as string;

        expect(writtenContent).toContain('# Guidelines');
        expect(writtenContent).toContain('Some introductory text here.');
        expect(writtenContent).toContain('## Section Two');
        expect(writtenContent).toContain('More prose below.');
    });

    it('should skip orphan cleanup when .workspaces does not exist', () => {
        vi.mocked(existsSync).mockReturnValue(false);
        vi.mocked(readFileSync).mockReturnValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionWriter('/ext', configManager);
        writer.removeOrphanedStagingDirs();

        expect(readdirSync).not.toHaveBeenCalled();
        expect(rmSync).not.toHaveBeenCalled();
    });

    it('should remove stale staging dirs older than 1 hour', () => {
        vi.mocked(existsSync).mockReturnValue(true);
        vi.mocked(readdirSync).mockReturnValue(['stale_hash_01'] as unknown as ReturnType<typeof readdirSync>);
        vi.mocked(statSync).mockReturnValue({ mtimeMs: Date.now() - 2 * 60 * 60 * 1000 } as import('node:fs').Stats);
        vi.mocked(readFileSync).mockReturnValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionWriter('/ext', configManager);
        writer.removeOrphanedStagingDirs();

        expect(rmSync).toHaveBeenCalledWith(
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
        const writer = new InstructionWriter('/ext', configManager);
        writer.removeOrphanedStagingDirs();

        expect(rmSync).not.toHaveBeenCalled();
    });

    it('should dispose debounce timer and subscriptions', () => {
        vi.mocked(readFileSync).mockReturnValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionWriter('/ext', configManager);

        // Should not throw.
        writer.dispose();
    });
});
