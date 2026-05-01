import { describe, it, expect, vi, beforeEach } from 'vitest';

import { AutoContextProjector } from '#src/autocontext-projector.js';
import { McpCategoryEntry } from '#src/mcp-category-entry';
import { McpToolEntry } from '#src/mcp-tool-entry';
import { McpToolsManifest } from '#src/mcp-tools-manifest';
import { createFakeLogger, createMockConfigManager } from '#testing/fakes';
import { makeInstructionsFilesManifest, projectorTestInstructions } from '#testing/fixtures';
import { findSetContextCall } from '#testing/utils';

beforeEach(() => {
    vi.clearAllMocks();
});

function buildProjectorManifest(): McpToolsManifest {
    const dotnet = new McpCategoryEntry('.NET', undefined, 'dotnet', []);
    const workspace = new McpCategoryEntry('Workspace', undefined, 'workspace', []);
    const tools: McpToolEntry[] = [
        new McpToolEntry('analyze_csharp_code', undefined, [dotnet], [
            { name: 'analyze_csharp_coding_style' },
            { name: 'analyze_csharp_async_patterns' },
        ]),
        new McpToolEntry('read_editorconfig', undefined, [workspace], [
            { name: 'get_editorconfig_rules' },
        ]),
    ];
    return new McpToolsManifest(tools, [dotnet, workspace]);
}

describe('AutoContextProjector', () => {
    const catalog = makeInstructionsFilesManifest(projectorTestInstructions);
    const toolsManifest = buildProjectorManifest();
    const logger = createFakeLogger();

    it('should set all context keys to true when config is empty', async () => {
        const projector = new AutoContextProjector(createMockConfigManager({}), catalog, toolsManifest, logger);
        await projector.project();

        expect(findSetContextCall('autocontext.instructions.code-review')?.[2]).toBe(true);
        expect(findSetContextCall('autocontext.instructions.lang-csharp')?.[2]).toBe(true);
        expect(findSetContextCall('autocontext.mcpTools.analyze_csharp_code.analyze_csharp_coding_style')?.[2]).toBe(true);
        expect(findSetContextCall('autocontext.mcpTools.analyze_csharp_code.analyze_csharp_async_patterns')?.[2]).toBe(true);
        expect.soft(findSetContextCall('autocontext.mcpTools.read_editorconfig.get_editorconfig_rules')?.[2]).toBe(true);
    });

    it('should set instruction context key to false when disabled', async () => {
        const projector = new AutoContextProjector(
            createMockConfigManager({
                instructions: { 'code-review.instructions.md': { enabled: false } },
            }),
            catalog,
            toolsManifest,
            logger,
        );
        await projector.project();

        expect(findSetContextCall('autocontext.instructions.code-review')?.[2]).toBe(false);
        expect.soft(findSetContextCall('autocontext.instructions.lang-csharp')?.[2]).toBe(true);
    });

    it('should set task context key to false when parent disables the task', async () => {
        const projector = new AutoContextProjector(
            createMockConfigManager({
                mcpTools: { read_editorconfig: { disabledTasks: ['get_editorconfig_rules'] } },
            }),
            catalog,
            toolsManifest,
            logger,
        );
        await projector.project();

        expect(findSetContextCall('autocontext.mcpTools.read_editorconfig.get_editorconfig_rules')?.[2]).toBe(false);
        expect.soft(findSetContextCall('autocontext.mcpTools.analyze_csharp_code.analyze_csharp_coding_style')?.[2]).toBe(true);
    });

    it('should set task context key to false when in disabledTasks list', async () => {
        const projector = new AutoContextProjector(
            createMockConfigManager({
                mcpTools: { analyze_csharp_code: { disabledTasks: ['analyze_csharp_coding_style'] } },
            }),
            catalog,
            toolsManifest,
            logger,
        );
        await projector.project();

        expect(findSetContextCall('autocontext.mcpTools.analyze_csharp_code.analyze_csharp_coding_style')?.[2]).toBe(false);
        expect.soft(findSetContextCall('autocontext.mcpTools.analyze_csharp_code.analyze_csharp_async_patterns')?.[2]).toBe(true);
    });

    it('should disable all tasks when parent has enabled false', async () => {
        const projector = new AutoContextProjector(
            createMockConfigManager({
                mcpTools: {
                    analyze_csharp_code: {
                        enabled: false,
                        disabledTasks: ['analyze_csharp_coding_style', 'analyze_csharp_async_patterns'],
                    },
                },
            }),
            catalog,
            toolsManifest,
            logger,
        );
        await projector.project();

        expect(findSetContextCall('autocontext.mcpTools.analyze_csharp_code.analyze_csharp_coding_style')?.[2]).toBe(false);
        expect.soft(findSetContextCall('autocontext.mcpTools.analyze_csharp_code.analyze_csharp_async_patterns')?.[2]).toBe(false);
    });

    it('should keep instruction enabled when entry exists but is not disabled', async () => {
        const projector = new AutoContextProjector(
            createMockConfigManager({
                instructions: {
                    'code-review.instructions.md': { version: '0.5', disabledInstructions: ['INST0001'] },
                },
            }),
            catalog,
            toolsManifest,
            logger,
        );
        await projector.project();

        expect.soft(findSetContextCall('autocontext.instructions.code-review')?.[2]).toBe(true);
    });

    it('should log to logger when project fails via onDidChange', async () => {
        let onDidChangeCallback!: () => void;
        const failingManager = {
            read: vi.fn().mockRejectedValue(new Error('read boom')),
            onDidChange: vi.fn((cb: () => void) => { onDidChangeCallback = cb; return { dispose: vi.fn() }; }),
        } as unknown as import('../../src/autocontext-config-manager').AutoContextConfigManager;

        const oc = createFakeLogger();
        const _projector = new AutoContextProjector(failingManager, catalog, toolsManifest, oc);

        onDidChangeCallback();
        await vi.waitFor(() => {
            expect(oc.error).toHaveBeenCalledWith(
                'Failed to project config',
                expect.objectContaining({ message: 'read boom' }),
            );
        });

        _projector.dispose();
    });
});
