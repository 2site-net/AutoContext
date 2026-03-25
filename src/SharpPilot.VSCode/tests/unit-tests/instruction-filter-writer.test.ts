import { describe, it, expect, vi, beforeEach } from 'vitest';

import { InstructionFilterWriter } from '../../src/instruction-filter-writer';
import { SharpPilotConfigManager } from '../../src/sharppilot-config';
import { parseInstructions } from '../../src/instruction-parser';
import { instructions, filteredContextKey } from '../../src/config';

import { readFileSync, writeFileSync, existsSync, readdirSync, rmSync, statSync, unlinkSync } from 'node:fs';

vi.mock('node:fs', () => ({
    readFileSync: vi.fn(),
    writeFileSync: vi.fn(),
    mkdirSync: vi.fn(),
    existsSync: vi.fn(() => false),
    readdirSync: vi.fn(() => []),
    rmSync: vi.fn(),
    statSync: vi.fn(),
    unlinkSync: vi.fn(),
}));

import { workspace, commands } from './__mocks__/vscode';

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

describe('InstructionFilterWriter', () => {
    it('should not write any files when no instructions are disabled', () => {
        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionFilterWriter('/ext', configManager);
        writer.write();

        expect(writeFileSync).not.toHaveBeenCalled();
    });

    it('should delete stale live files when no instructions are disabled', () => {
        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionFilterWriter('/ext', configManager);
        writer.write();

        // Should attempt to delete every live instruction file.
        expect(unlinkSync).toHaveBeenCalled();
        const unlinkCalls = vi.mocked(unlinkSync).mock.calls;
        for (const entry of instructions) {
            expect(unlinkCalls).toContainEqual(
                [expect.stringContaining(entry.fileName)],
            );
        }
    });

    it('should set filtered context keys to false when no instructions are disabled', () => {
        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return testContent;
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionFilterWriter('/ext', configManager);
        writer.write();

        for (const entry of instructions) {
            expect(commands.executeCommand).toHaveBeenCalledWith(
                'setContext',
                filteredContextKey(entry.settingId),
                false,
            );
        }
    });

    it('should set filtered context key to true when instructions are disabled', () => {
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
        const writer = new InstructionFilterWriter('/ext', configManager);
        writer.write();

        expect(commands.executeCommand).toHaveBeenCalledWith(
            'setContext',
            filteredContextKey(instructions[0].settingId),
            true,
        );
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
        const writer = new InstructionFilterWriter('/ext', configManager);
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
    });

    it('should skip orphan cleanup when .workspaces does not exist', () => {
        vi.mocked(existsSync).mockReturnValue(false);
        vi.mocked(readFileSync).mockReturnValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionFilterWriter('/ext', configManager);
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
        const writer = new InstructionFilterWriter('/ext', configManager);
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
        const writer = new InstructionFilterWriter('/ext', configManager);
        writer.removeOrphanedStagingDirs();

        expect(rmSync).not.toHaveBeenCalled();
    });

    it('should dispose debounce timer and subscriptions', () => {
        vi.mocked(readFileSync).mockReturnValue('{}');

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new InstructionFilterWriter('/ext', configManager);

        // Should not throw.
        writer.dispose();
    });
});
