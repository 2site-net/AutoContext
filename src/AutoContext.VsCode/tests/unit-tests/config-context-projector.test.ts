import { describe, it, expect, vi, beforeEach } from 'vitest';
import { commands } from './__mocks__/vscode';

import { ConfigContextProjector } from '../../src/config-context-projector';
import { InstructionsCatalog } from '../../src/instructions-catalog';
import { McpToolsCatalog } from '../../src/mcp-tools-catalog';
import type { AutoContextConfig } from '../../src/types/autocontext-config';
import type { AutoContextConfigManager } from '../../src/autocontext-config';
import type { InstructionsFileEntry } from '../../src/types/instructions-file-entry';
import type { McpToolsEntry } from '../../src/types/mcp-tools-entry';

const testInstructions: InstructionsFileEntry[] = [
    { key: 'codeReview', fileName: 'code-review.instructions.md', label: 'Code Review', category: 'General' },
    { key: 'lang.csharp', fileName: 'lang-csharp.instructions.md', label: 'C#', category: 'Languages', workspaceFlags: ['hasCSharp'] },
];

const testTools: McpToolsEntry[] = [
    { key: 'check_csharp_coding_style', toolName: 'check_csharp_all', label: 'C# Coding Style', category: '.NET', serverLabel: '.NET', scope: 'dotnet', workspaceFlags: ['hasCSharp'] },
    { key: 'check_csharp_async_patterns', toolName: 'check_csharp_all', label: 'C# Async', category: '.NET', serverLabel: '.NET', scope: 'dotnet', workspaceFlags: ['hasCSharp'] },
    { key: 'get_editorconfig', label: 'EditorConfig', category: 'Workspace', serverLabel: 'Workspace', scope: 'editorconfig' },
];

function createMockConfigManager(config: AutoContextConfig): AutoContextConfigManager {
    return {
        read: vi.fn().mockResolvedValue(config),
        onDidChange: vi.fn(() => ({ dispose: vi.fn() })),
    } as unknown as AutoContextConfigManager;
}

function findSetContextCall(key: string): [string, string, boolean] | undefined {
    return vi.mocked(commands.executeCommand).mock.calls
        .find(c => c[0] === 'setContext' && c[1] === key) as [string, string, boolean] | undefined;
}

beforeEach(() => {
    vi.clearAllMocks();
});

describe('ConfigContextProjector', () => {
    const catalog = new InstructionsCatalog(testInstructions);
    const toolsCatalog = new McpToolsCatalog(testTools);

    it('should set all context keys to true when config is empty', async () => {
        const projector = new ConfigContextProjector(createMockConfigManager({}), catalog, toolsCatalog);
        await projector.project();

        expect(findSetContextCall('autocontext.instructions.codeReview')?.[2]).toBe(true);
        expect(findSetContextCall('autocontext.instructions.lang.csharp')?.[2]).toBe(true);
        expect(findSetContextCall('autocontext.mcpTools.check_csharp_coding_style')?.[2]).toBe(true);
        expect(findSetContextCall('autocontext.mcpTools.check_csharp_async_patterns')?.[2]).toBe(true);
        expect.soft(findSetContextCall('autocontext.mcpTools.get_editorconfig')?.[2]).toBe(true);
    });

    it('should set instruction context key to false when disabled', async () => {
        const projector = new ConfigContextProjector(
            createMockConfigManager({
                instructions: { 'code-review.instructions.md': { enabled: false } },
            }),
            catalog,
            toolsCatalog,
        );
        await projector.project();

        expect(findSetContextCall('autocontext.instructions.codeReview')?.[2]).toBe(false);
        expect.soft(findSetContextCall('autocontext.instructions.lang.csharp')?.[2]).toBe(true);
    });

    it('should set standalone tool context key to false when disabled', async () => {
        const projector = new ConfigContextProjector(
            createMockConfigManager({ mcpTools: { get_editorconfig: false } }),
            catalog,
            toolsCatalog,
        );
        await projector.project();

        expect(findSetContextCall('autocontext.mcpTools.get_editorconfig')?.[2]).toBe(false);
        expect.soft(findSetContextCall('autocontext.mcpTools.check_csharp_coding_style')?.[2]).toBe(true);
    });

    it('should set feature context key to false when in disabled-features list', async () => {
        const projector = new ConfigContextProjector(
            createMockConfigManager({
                mcpTools: { check_csharp_all: { disabledFeatures: ['check_csharp_coding_style'] } },
            }),
            catalog,
            toolsCatalog,
        );
        await projector.project();

        expect(findSetContextCall('autocontext.mcpTools.check_csharp_coding_style')?.[2]).toBe(false);
        expect.soft(findSetContextCall('autocontext.mcpTools.check_csharp_async_patterns')?.[2]).toBe(true);
    });

    it('should disable all features when parent has enabled false', async () => {
        const projector = new ConfigContextProjector(
            createMockConfigManager({
                mcpTools: {
                    check_csharp_all: {
                        enabled: false,
                        disabledFeatures: ['check_csharp_coding_style', 'check_csharp_async_patterns'],
                    },
                },
            }),
            catalog,
            toolsCatalog,
        );
        await projector.project();

        expect(findSetContextCall('autocontext.mcpTools.check_csharp_coding_style')?.[2]).toBe(false);
        expect.soft(findSetContextCall('autocontext.mcpTools.check_csharp_async_patterns')?.[2]).toBe(false);
    });

    it('should keep instruction enabled when entry exists but is not disabled', async () => {
        const projector = new ConfigContextProjector(
            createMockConfigManager({
                instructions: {
                    'code-review.instructions.md': { version: '0.5', disabledInstructions: ['INST0001'] },
                },
            }),
            catalog,
            toolsCatalog,
        );
        await projector.project();

        expect.soft(findSetContextCall('autocontext.instructions.codeReview')?.[2]).toBe(true);
    });
});

describe('ConfigContextProjector.isToolEnabled', () => {
    it('should return true when no tools config exists', () => {
        expect.soft(ConfigContextProjector.isToolEnabled({}, 'check_csharp_all', 'check_csharp_coding_style')).toBe(true);
    });

    it('should return false for standalone tool set to false', () => {
        expect.soft(ConfigContextProjector.isToolEnabled(
            { mcpTools: { get_editorconfig: false } }, 'get_editorconfig',
        )).toBe(false);
    });

    it('should return false for tool with enabled false', () => {
        expect.soft(ConfigContextProjector.isToolEnabled(
            { mcpTools: { get_editorconfig: { enabled: false } } }, 'get_editorconfig',
        )).toBe(false);
    });

    it('should return false for feature when parent disabled', () => {
        expect.soft(ConfigContextProjector.isToolEnabled(
            { mcpTools: { check_csharp_all: { enabled: false, disabledFeatures: ['check_csharp_coding_style'] } } },
            'check_csharp_all', 'check_csharp_coding_style',
        )).toBe(false);
    });

    it('should return false for feature in disabled list', () => {
        expect.soft(ConfigContextProjector.isToolEnabled(
            { mcpTools: { check_csharp_all: { disabledFeatures: ['check_csharp_coding_style'] } } },
            'check_csharp_all', 'check_csharp_coding_style',
        )).toBe(false);
    });

    it('should return true for feature not in disabled list', () => {
        expect.soft(ConfigContextProjector.isToolEnabled(
            { mcpTools: { check_csharp_all: { disabledFeatures: ['check_csharp_async_patterns'] } } },
            'check_csharp_all', 'check_csharp_coding_style',
        )).toBe(true);
    });

    it('should return true for standalone tool with no config entry', () => {
        expect.soft(ConfigContextProjector.isToolEnabled(
            { mcpTools: { check_csharp_all: false } }, 'get_editorconfig',
        )).toBe(true);
    });
});
