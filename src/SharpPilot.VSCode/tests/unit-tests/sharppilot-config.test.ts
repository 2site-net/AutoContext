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
                disabled: {
                    'code-review.instructions.md': ['INST0001'],
                },
            },
        }));

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        const disabled = manager.getDisabledInstructions('code-review.instructions.md');

        expect(disabled.has('INST0001')).toBe(true);
        expect(disabled.size).toBe(1);
    });

    it('should return empty set for file with no disabled instructions', () => {
        vi.mocked(readFileSync).mockReturnValue('{}');

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        const disabled = manager.getDisabledInstructions('code-review.instructions.md');

        expect(disabled.size).toBe(0);
    });

    it('should detect when any instructions are disabled', () => {
        vi.mocked(readFileSync).mockReturnValue(JSON.stringify({
            instructions: {
                disabled: {
                    'code-review.instructions.md': ['INST0001'],
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
        manager.toggleInstruction('code-review.instructions.md', 'INST0001');

        const writeCalls = vi.mocked(writeFileSync).mock.calls;

        expect(writeCalls).toHaveLength(1);

        const [path, content] = writeCalls[0];

        expect(path).toMatch(/\.sharppilot\.json$/);

        const parsed = JSON.parse(content as string);

        expect(parsed.instructions.disabled['code-review.instructions.md']).toEqual(['INST0001']);
    });

    it('should toggle an instruction off (re-enable it)', () => {
        vi.mocked(readFileSync).mockReturnValue(JSON.stringify({
            instructions: {
                disabled: {
                    'code-review.instructions.md': ['INST0001'],
                },
            },
        }));

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.toggleInstruction('code-review.instructions.md', 'INST0001');

        // When all instructions re-enabled, file should be deleted (empty config).
        expect(vi.mocked(unlinkSync)).toHaveBeenCalled();
    });

    it('should not write when no workspace folder is available', () => {
        workspace.workspaceFolders = undefined;

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.toggleInstruction('code-review.instructions.md', 'INST0001');

        expect(vi.mocked(writeFileSync)).not.toHaveBeenCalled();
    });

    it('should write extension version when saving config', () => {
        vi.mocked(readFileSync).mockReturnValue('{}');

        const manager = new SharpPilotConfigManager('/ext', '1.2.3');
        manager.toggleInstruction('code-review.instructions.md', 'INST0001');

        const writeCalls = vi.mocked(writeFileSync).mock.calls;
        const parsed = JSON.parse(writeCalls[0][1] as string);

        expect(parsed.version).toBe('1.2.3');
    });

    it('should reset all instructions for a specific file', () => {
        vi.mocked(readFileSync).mockReturnValue(JSON.stringify({
            instructions: {
                disabled: {
                    'code-review.instructions.md': ['INST0001', 'INST0002'],
                    'dotnet-async-await.instructions.md': ['INST0003'],
                },
            },
        }));

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.resetInstructions('code-review.instructions.md');

        const writeCalls = vi.mocked(writeFileSync).mock.calls;

        expect(writeCalls).toHaveLength(1);

        const parsed = JSON.parse(writeCalls[0][1] as string);

        expect(parsed.instructions.disabled['code-review.instructions.md']).toBeUndefined();
        expect(parsed.instructions.disabled['dotnet-async-await.instructions.md']).toEqual(['INST0003']);
    });

    it('should delete file when resetting the last file with disabled instructions', () => {
        vi.mocked(readFileSync).mockReturnValue(JSON.stringify({
            instructions: {
                disabled: {
                    'code-review.instructions.md': ['INST0001'],
                },
            },
        }));

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.resetInstructions('code-review.instructions.md');

        expect(vi.mocked(unlinkSync)).toHaveBeenCalled();
    });

    it('should be a no-op when resetting a file with no disabled instructions', () => {
        vi.mocked(readFileSync).mockReturnValue('{}');

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.resetInstructions('code-review.instructions.md');

        expect(vi.mocked(writeFileSync)).not.toHaveBeenCalled();
        expect(vi.mocked(unlinkSync)).not.toHaveBeenCalled();
    });

    it('should delete file when toggling last instruction off with version present (round-trip)', () => {
        vi.mocked(readFileSync).mockReturnValue(JSON.stringify({
            version: '0.5.0',
            instructions: {
                disabled: {
                    'code-review.instructions.md': ['INST0001'],
                },
            },
        }));

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.toggleInstruction('code-review.instructions.md', 'INST0001');

        expect(vi.mocked(unlinkSync)).toHaveBeenCalled();
        expect(vi.mocked(writeFileSync)).not.toHaveBeenCalled();
    });

    it('should write disabled tools to config', () => {
        vi.mocked(readFileSync).mockReturnValue('{}');

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.setDisabledTools(['check_csharp_coding_style']);

        const writeCalls = vi.mocked(writeFileSync).mock.calls;

        expect(writeCalls).toHaveLength(1);

        const parsed = JSON.parse(writeCalls[0][1] as string);

        expect(parsed["mcp-tools"].disabled).toEqual(['check_csharp_coding_style']);
    });

    it('should skip write when disabled tools have not changed', () => {
        vi.mocked(readFileSync).mockReturnValue(JSON.stringify({
            "mcp-tools": { disabled: ['check_csharp_coding_style'] },
        }));

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.setDisabledTools(['check_csharp_coding_style']);

        expect(vi.mocked(writeFileSync)).not.toHaveBeenCalled();
        expect(vi.mocked(unlinkSync)).not.toHaveBeenCalled();
    });

    it('should remove tools section when all tools are enabled', () => {
        vi.mocked(readFileSync).mockReturnValue(JSON.stringify({
            "mcp-tools": { disabled: ['check_csharp_coding_style'] },
            instructions: { disabled: { 'code-review.instructions.md': ['INST0001'] } },
        }));

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.setDisabledTools([]);

        const writeCalls = vi.mocked(writeFileSync).mock.calls;

        expect(writeCalls).toHaveLength(1);

        const parsed = JSON.parse(writeCalls[0][1] as string);

        expect(parsed["mcp-tools"]).toBeUndefined();
        expect(parsed.instructions).toBeDefined();
    });

    it('should delete file when clearing tools and no other config exists', () => {
        vi.mocked(readFileSync).mockReturnValue(JSON.stringify({
            "mcp-tools": { disabled: ['check_csharp_coding_style'] },
        }));

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.setDisabledTools([]);

        expect(vi.mocked(unlinkSync)).toHaveBeenCalled();
        expect(vi.mocked(writeFileSync)).not.toHaveBeenCalled();
    });

    it('should preserve other config sections when writing tools', () => {
        vi.mocked(readFileSync).mockReturnValue(JSON.stringify({
            instructions: { disabled: { 'code-review.instructions.md': ['INST0001'] } },
        }));

        const manager = new SharpPilotConfigManager('/ext', '0.5.0');
        manager.setDisabledTools(['check_csharp_async_patterns']);

        const parsed = JSON.parse(vi.mocked(writeFileSync).mock.calls[0][1] as string);

        expect(parsed["mcp-tools"].disabled).toEqual(['check_csharp_async_patterns']);
        expect(parsed.instructions.disabled['code-review.instructions.md']).toEqual(['INST0001']);
    });
});
