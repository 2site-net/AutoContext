import { describe, it, expect, vi, beforeEach } from 'vitest';

import { AutoContextConfigManager } from '../../src/autocontext-config';

import { writeFile, unlink, readFile } from 'node:fs/promises';

vi.mock('node:fs/promises', () => ({
    readFile: vi.fn().mockRejectedValue(new Error('ENOENT')),
    writeFile: vi.fn().mockResolvedValue(undefined),
    unlink: vi.fn().mockResolvedValue(undefined),
}));

import { workspace } from './__mocks__/vscode';

beforeEach(() => {
    vi.clearAllMocks();
    workspace.workspaceFolders = [{ uri: { fsPath: '/workspace' } }];
});

describe('AutoContextConfigManager', () => {
    it('should return empty config when file does not exist', async () => {
        vi.mocked(readFile).mockRejectedValue(new Error('ENOENT'));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        const config = await manager.read();

        expect.soft(config).toEqual({});
    });

    it('should return empty config when file contains invalid JSON', async () => {
        vi.mocked(readFile).mockResolvedValue('not json at all');

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        const config = await manager.read();

        expect.soft(config).toEqual({});
    });

    it('should read disabled instructions from config file', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            instructions: {
                disabled: {
                    'code-review.instructions.md': ['INST0001'],
                },
            },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        const disabled = await manager.getDisabledInstructions('code-review.instructions.md');

        expect(disabled.has('INST0001')).toBe(true);
        expect.soft(disabled.size).toBe(1);
    });

    it('should return empty set for file with no disabled instructions', async () => {
        vi.mocked(readFile).mockResolvedValue('{}');

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        const disabled = await manager.getDisabledInstructions('code-review.instructions.md');

        expect.soft(disabled.size).toBe(0);
    });

    it('should detect when any instructions are disabled', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            instructions: {
                disabled: {
                    'code-review.instructions.md': ['INST0001'],
                },
            },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');

        expect.soft(await manager.hasAnyDisabledInstructions()).toBe(true);
    });

    it('should detect when no instructions are disabled', async () => {
        vi.mocked(readFile).mockResolvedValue('{}');

        const manager = new AutoContextConfigManager('/ext', '0.5.0');

        expect.soft(await manager.hasAnyDisabledInstructions()).toBe(false);
    });

    it('should toggle an instruction on (disable it)', async () => {
        vi.mocked(readFile).mockResolvedValue('{}');

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.toggleInstruction('code-review.instructions.md', 'INST0001');

        const writeCalls = vi.mocked(writeFile).mock.calls;

        expect(writeCalls).toHaveLength(1);

        const [path, content] = writeCalls[0];

        expect(path).toMatch(/\.autocontext\.json$/);

        const parsed = JSON.parse(content as string);

        expect.soft(parsed.instructions.disabled['code-review.instructions.md']).toEqual(['INST0001']);
    });

    it('should toggle an instruction off (re-enable it)', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            instructions: {
                disabled: {
                    'code-review.instructions.md': ['INST0001'],
                },
            },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.toggleInstruction('code-review.instructions.md', 'INST0001');

        expect.soft(vi.mocked(unlink)).toHaveBeenCalled();
    });

    it('should not write when no workspace folder is available', async () => {
        workspace.workspaceFolders = undefined;

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.toggleInstruction('code-review.instructions.md', 'INST0001');

        expect.soft(vi.mocked(writeFile)).not.toHaveBeenCalled();
    });

    it('should write extension version when saving config', async () => {
        vi.mocked(readFile).mockResolvedValue('{}');

        const manager = new AutoContextConfigManager('/ext', '1.2.3');
        await manager.toggleInstruction('code-review.instructions.md', 'INST0001');

        const writeCalls = vi.mocked(writeFile).mock.calls;
        const parsed = JSON.parse(writeCalls[0][1] as string);

        expect.soft(parsed.version).toBe('1.2.3');
    });

    it('should reset all instructions for a specific file', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            instructions: {
                disabled: {
                    'code-review.instructions.md': ['INST0001', 'INST0002'],
                    'dotnet-async-await.instructions.md': ['INST0003'],
                },
            },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.resetInstructions('code-review.instructions.md');

        const writeCalls = vi.mocked(writeFile).mock.calls;

        expect(writeCalls).toHaveLength(1);

        const parsed = JSON.parse(writeCalls[0][1] as string);

        expect(parsed.instructions.disabled['code-review.instructions.md']).toBeUndefined();
        expect.soft(parsed.instructions.disabled['dotnet-async-await.instructions.md']).toEqual(['INST0003']);
    });

    it('should delete file when resetting the last file with disabled instructions', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            instructions: {
                disabled: {
                    'code-review.instructions.md': ['INST0001'],
                },
            },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.resetInstructions('code-review.instructions.md');

        expect.soft(vi.mocked(unlink)).toHaveBeenCalled();
    });

    it('should be a no-op when resetting a file with no disabled instructions', async () => {
        vi.mocked(readFile).mockResolvedValue('{}');

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.resetInstructions('code-review.instructions.md');

        expect(vi.mocked(writeFile)).not.toHaveBeenCalled();
        expect.soft(vi.mocked(unlink)).not.toHaveBeenCalled();
    });

    it('should delete file when toggling last instruction off with version present (round-trip)', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            version: '0.5.0',
            instructions: {
                disabled: {
                    'code-review.instructions.md': ['INST0001'],
                },
            },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.toggleInstruction('code-review.instructions.md', 'INST0001');

        expect(vi.mocked(unlink)).toHaveBeenCalled();
        expect.soft(vi.mocked(writeFile)).not.toHaveBeenCalled();
    });

    it('should write disabled tools to config', async () => {
        vi.mocked(readFile).mockResolvedValue('{}');

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.setDisabledTools(['check_csharp_coding_style']);

        const writeCalls = vi.mocked(writeFile).mock.calls;

        expect(writeCalls).toHaveLength(1);

        const parsed = JSON.parse(writeCalls[0][1] as string);

        expect.soft(parsed["mcp-tools"].disabled).toEqual(['check_csharp_coding_style']);
    });

    it('should skip write when disabled tools have not changed', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            "mcp-tools": { disabled: ['check_csharp_coding_style'] },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.setDisabledTools(['check_csharp_coding_style']);

        expect(vi.mocked(writeFile)).not.toHaveBeenCalled();
        expect.soft(vi.mocked(unlink)).not.toHaveBeenCalled();
    });

    it('should remove tools section when all tools are enabled', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            "mcp-tools": { disabled: ['check_csharp_coding_style'] },
            instructions: { disabled: { 'code-review.instructions.md': ['INST0001'] } },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.setDisabledTools([]);

        const writeCalls = vi.mocked(writeFile).mock.calls;

        expect(writeCalls).toHaveLength(1);

        const parsed = JSON.parse(writeCalls[0][1] as string);

        expect(parsed["mcp-tools"]).toBeUndefined();
        expect.soft(parsed.instructions).toBeDefined();
    });

    it('should delete file when clearing tools and no other config exists', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            "mcp-tools": { disabled: ['check_csharp_coding_style'] },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.setDisabledTools([]);

        expect(vi.mocked(unlink)).toHaveBeenCalled();
        expect.soft(vi.mocked(writeFile)).not.toHaveBeenCalled();
    });

    it('should preserve other config sections when writing tools', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            instructions: { disabled: { 'code-review.instructions.md': ['INST0001'] } },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.setDisabledTools(['check_csharp_async_patterns']);

        const parsed = JSON.parse(vi.mocked(writeFile).mock.calls[0][1] as string);

        expect(parsed["mcp-tools"].disabled).toEqual(['check_csharp_async_patterns']);
        expect.soft(parsed.instructions.disabled['code-review.instructions.md']).toEqual(['INST0001']);
    });

    it('should return cached config on repeated reads without re-reading disk', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            instructions: { disabled: { 'code-review.instructions.md': ['INST0001'] } },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.read();
        await manager.read();
        await manager.read();

        expect.soft(readFile).toHaveBeenCalledTimes(1);
    });

    it('should invalidate cache after writing config', async () => {
        vi.mocked(readFile).mockResolvedValue('{}');

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.read();
        await manager.toggleInstruction('code-review.instructions.md', 'INST0001');

        // toggleInstruction calls read() (cache hit) then writeConfig() (invalidates).
        // Next read() should re-read from disk.
        await manager.read();

        expect.soft(readFile).toHaveBeenCalledTimes(2);
    });

    it('should read disabled instructions from versioned format', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            instructions: {
                disabled: {
                    'code-review.instructions.md': { version: '1.0', ids: ['INST0001', 'INST0002'] },
                },
            },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        const disabled = await manager.getDisabledInstructions('code-review.instructions.md');

        expect(disabled.has('INST0001')).toBe(true);
        expect(disabled.has('INST0002')).toBe(true);
        expect.soft(disabled.size).toBe(2);
    });

    it('should detect disabled instructions in versioned format', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            instructions: {
                disabled: {
                    'code-review.instructions.md': { version: '1.0', ids: ['INST0001'] },
                },
            },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');

        expect.soft(await manager.hasAnyDisabledInstructions()).toBe(true);
    });

    it('should write versioned format when instruction version is provided', async () => {
        vi.mocked(readFile).mockResolvedValue('{}');

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.toggleInstruction('code-review.instructions.md', 'INST0001', '1.2.3');

        const writeCalls = vi.mocked(writeFile).mock.calls;
        const parsed = JSON.parse(writeCalls[0][1] as string);

        expect.soft(parsed.instructions.disabled['code-review.instructions.md']).toEqual({
            version: '1.2',
            ids: ['INST0001'],
        });
    });

    it('should write plain array when no instruction version is provided', async () => {
        vi.mocked(readFile).mockResolvedValue('{}');

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.toggleInstruction('code-review.instructions.md', 'INST0001');

        const writeCalls = vi.mocked(writeFile).mock.calls;
        const parsed = JSON.parse(writeCalls[0][1] as string);

        expect.soft(parsed.instructions.disabled['code-review.instructions.md']).toEqual(['INST0001']);
    });

    it('should clear stale disabled IDs when MINOR version advances', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            instructions: {
                disabled: {
                    'code-review.instructions.md': { version: '1.0', ids: ['INST0001'] },
                    'lang-csharp.instructions.md': { version: '1.0', ids: ['INST0005'] },
                },
            },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        const cleared = await manager.clearStaleDisabledIds(new Map([
            ['code-review.instructions.md', '1.1.0'],
            ['lang-csharp.instructions.md', '1.0.5'],
        ]));

        expect.soft(cleared).toEqual(['code-review.instructions.md']);

        const writeCalls = vi.mocked(writeFile).mock.calls;
        const parsed = JSON.parse(writeCalls[0][1] as string);

        expect(parsed.instructions.disabled['code-review.instructions.md']).toBeUndefined();
        expect.soft(parsed.instructions.disabled['lang-csharp.instructions.md']).toEqual({
            version: '1.0',
            ids: ['INST0005'],
        });
    });

    it('should clear stale disabled IDs when MAJOR version advances', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            instructions: {
                disabled: {
                    'code-review.instructions.md': { version: '1.0', ids: ['INST0001'] },
                },
            },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        const cleared = await manager.clearStaleDisabledIds(new Map([
            ['code-review.instructions.md', '2.0.0'],
        ]));

        expect.soft(cleared).toEqual(['code-review.instructions.md']);
        expect.soft(vi.mocked(unlink)).toHaveBeenCalled();
    });

    it('should return empty array when no stale disabled IDs exist', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            instructions: {
                disabled: {
                    'code-review.instructions.md': { version: '1.0', ids: ['INST0001'] },
                },
            },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        const cleared = await manager.clearStaleDisabledIds(new Map([
            ['code-review.instructions.md', '1.0.5'],
        ]));

        expect(cleared).toEqual([]);
        expect(vi.mocked(writeFile)).not.toHaveBeenCalled();
        expect.soft(vi.mocked(unlink)).not.toHaveBeenCalled();
    });

    it('should toggle off from versioned format preserving version', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            instructions: {
                disabled: {
                    'code-review.instructions.md': { version: '1.0', ids: ['INST0001', 'INST0002'] },
                },
            },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.toggleInstruction('code-review.instructions.md', 'INST0001', '1.0.3');

        const writeCalls = vi.mocked(writeFile).mock.calls;
        const parsed = JSON.parse(writeCalls[0][1] as string);

        expect.soft(parsed.instructions.disabled['code-review.instructions.md']).toEqual({
            version: '1.0',
            ids: ['INST0002'],
        });
    });

    it('should preserve version when toggling without instructionVersion param', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            instructions: {
                disabled: {
                    'code-review.instructions.md': { version: '1.0', ids: ['INST0001', 'INST0002'] },
                },
            },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        await manager.toggleInstruction('code-review.instructions.md', 'INST0001');

        const writeCalls = vi.mocked(writeFile).mock.calls;
        const parsed = JSON.parse(writeCalls[0][1] as string);

        expect.soft(parsed.instructions.disabled['code-review.instructions.md']).toEqual({
            version: '1.0',
            ids: ['INST0002'],
        });
    });

    it('should skip non-versioned entries during stale check', async () => {
        vi.mocked(readFile).mockResolvedValue(JSON.stringify({
            instructions: {
                disabled: {
                    'code-review.instructions.md': ['INST0001'],
                },
            },
        }));

        const manager = new AutoContextConfigManager('/ext', '0.5.0');
        const cleared = await manager.clearStaleDisabledIds(new Map([
            ['code-review.instructions.md', '2.0.0'],
        ]));

        expect(cleared).toEqual([]);
        expect(vi.mocked(writeFile)).not.toHaveBeenCalled();
        expect.soft(vi.mocked(unlink)).not.toHaveBeenCalled();
    });
});
