import { describe, it, expect, vi, beforeEach } from 'vitest';

import { SharpPilotConfigManager } from '../../src/sharppilot-config';

import { readFileSync, writeFileSync, unlinkSync } from 'node:fs';

vi.mock('node:fs', () => ({
    readFileSync: vi.fn(),
    writeFileSync: vi.fn(),
    unlinkSync: vi.fn(),
}));

import { workspace } from './__mocks__/vscode';

beforeEach(() => {
    vi.clearAllMocks();
    workspace.workspaceFolders = [{ uri: { fsPath: '/workspace' } }];
});

describe('SharpPilotConfigManager', () => {
    it('should return empty config when file does not exist', () => {
        vi.mocked(readFileSync).mockImplementation(() => { throw new Error('ENOENT'); });

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        const config = manager.read();

        expect(config).toEqual({});
    });

    it('should return empty config when file contains invalid JSON', () => {
        vi.mocked(readFileSync).mockReturnValue('not json at all');

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        const config = manager.read();

        expect(config).toEqual({});
    });

    it('should read disabled instructions from config file', () => {
        vi.mocked(readFileSync).mockReturnValue(JSON.stringify({
            instructions: {
                disabledInstructions: {
                    'copilot.instructions.md': ['abc123def456'],
                },
            },
        }));

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        const disabled = manager.getDisabledInstructions('copilot.instructions.md');

        expect(disabled.has('abc123def456')).toBe(true);
        expect(disabled.size).toBe(1);
    });

    it('should return empty set for file with no disabled instructions', () => {
        vi.mocked(readFileSync).mockReturnValue('{}');

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        const disabled = manager.getDisabledInstructions('copilot.instructions.md');

        expect(disabled.size).toBe(0);
    });

    it('should detect when any instructions are disabled', () => {
        vi.mocked(readFileSync).mockReturnValue(JSON.stringify({
            instructions: {
                disabledInstructions: {
                    'copilot.instructions.md': ['abc123def456'],
                },
            },
        }));

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');

        expect(manager.hasAnyDisabledInstructions()).toBe(true);
    });

    it('should detect when no instructions are disabled', () => {
        vi.mocked(readFileSync).mockReturnValue('{}');

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');

        expect(manager.hasAnyDisabledInstructions()).toBe(false);
    });

    it('should toggle an instruction on (disable it)', () => {
        vi.mocked(readFileSync).mockReturnValue('{}');

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.toggleInstruction('copilot.instructions.md', 'abc123def456');

        const writeCalls = vi.mocked(writeFileSync).mock.calls;

        expect(writeCalls).toHaveLength(1);

        const [path, content] = writeCalls[0];

        expect(path).toMatch(/\.sharppilot\.json$/);

        const parsed = JSON.parse(content as string);

        expect(parsed.instructions.disabledInstructions['copilot.instructions.md']).toEqual(['abc123def456']);
    });

    it('should toggle an instruction off (re-enable it)', () => {
        vi.mocked(readFileSync).mockReturnValue(JSON.stringify({
            instructions: {
                disabledInstructions: {
                    'copilot.instructions.md': ['abc123def456'],
                },
            },
        }));

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.toggleInstruction('copilot.instructions.md', 'abc123def456');

        // When all instructions re-enabled, file should be deleted (empty config).
        expect(vi.mocked(unlinkSync)).toHaveBeenCalled();
    });

    it('should not write when no workspace folder is available', () => {
        workspace.workspaceFolders = undefined;

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.toggleInstruction('copilot.instructions.md', 'abc123def456');

        expect(vi.mocked(writeFileSync)).not.toHaveBeenCalled();
    });

    it('should write extension version when saving config', () => {
        vi.mocked(readFileSync).mockReturnValue('{}');

        const manager = new SharpPilotConfigManager('/ext', '1.2.3');
        manager.toggleInstruction('copilot.instructions.md', 'abc123def456');

        const writeCalls = vi.mocked(writeFileSync).mock.calls;
        const parsed = JSON.parse(writeCalls[0][1] as string);

        expect(parsed.version).toBe('1.2.3');
    });

    it('should reset all instructions for a specific file', () => {
        vi.mocked(readFileSync).mockReturnValue(JSON.stringify({
            instructions: {
                disabledInstructions: {
                    'copilot.instructions.md': ['hash1', 'hash2'],
                    'dotnet-async-await.instructions.md': ['hash3'],
                },
            },
        }));

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.resetInstructions('copilot.instructions.md');

        const writeCalls = vi.mocked(writeFileSync).mock.calls;

        expect(writeCalls).toHaveLength(1);

        const parsed = JSON.parse(writeCalls[0][1] as string);

        expect(parsed.instructions.disabledInstructions['copilot.instructions.md']).toBeUndefined();
        expect(parsed.instructions.disabledInstructions['dotnet-async-await.instructions.md']).toEqual(['hash3']);
    });

    it('should delete file when resetting the last file with disabled instructions', () => {
        vi.mocked(readFileSync).mockReturnValue(JSON.stringify({
            instructions: {
                disabledInstructions: {
                    'copilot.instructions.md': ['hash1'],
                },
            },
        }));

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.resetInstructions('copilot.instructions.md');

        expect(vi.mocked(unlinkSync)).toHaveBeenCalled();
    });

    it('should be a no-op when resetting a file with no disabled instructions', () => {
        vi.mocked(readFileSync).mockReturnValue('{}');

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.resetInstructions('copilot.instructions.md');

        expect(vi.mocked(writeFileSync)).not.toHaveBeenCalled();
        expect(vi.mocked(unlinkSync)).not.toHaveBeenCalled();
    });

    it('should delete file when toggling last instruction off with version present (round-trip)', () => {
        vi.mocked(readFileSync).mockReturnValue(JSON.stringify({
            version: '0.5.0',
            instructions: {
                disabledInstructions: {
                    'copilot.instructions.md': ['abc123def456'],
                },
            },
        }));

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.toggleInstruction('copilot.instructions.md', 'abc123def456');

        expect(vi.mocked(unlinkSync)).toHaveBeenCalled();
        expect(vi.mocked(writeFileSync)).not.toHaveBeenCalled();
    });
});
