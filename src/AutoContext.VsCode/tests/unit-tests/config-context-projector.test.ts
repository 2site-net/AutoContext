import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ConfigContextProjector, isToolEnabled } from '../../src/config-context-projector';
import { InstructionsCatalog } from '../../src/instructions-catalog';
import { McpToolsCatalog } from '../../src/mcp-tools-catalog';
import { createFakeOutputChannel, createMockConfigManager } from './_fakes';
import { projectorTestInstructions, projectorTestTools } from './_fixtures';
import { findSetContextCall } from './_utils';

beforeEach(() => {
    vi.clearAllMocks();
});

describe('ConfigContextProjector', () => {
    const catalog = new InstructionsCatalog(projectorTestInstructions);
    const toolsCatalog = new McpToolsCatalog(projectorTestTools);
    const outputChannel = createFakeOutputChannel();

    it('should set all context keys to true when config is empty', async () => {
        const projector = new ConfigContextProjector(createMockConfigManager({}), catalog, toolsCatalog, outputChannel);
        await projector.project();

        expect(findSetContextCall('autocontext.instructions.codeReview')?.[2]).toBe(true);
        expect(findSetContextCall('autocontext.instructions.lang.csharp')?.[2]).toBe(true);
        expect(findSetContextCall('autocontext.mcpTools.analyze_csharp_coding_style')?.[2]).toBe(true);
        expect(findSetContextCall('autocontext.mcpTools.analyze_csharp_async_patterns')?.[2]).toBe(true);
        expect.soft(findSetContextCall('autocontext.mcpTools.get_editorconfig_rules')?.[2]).toBe(true);
    });

    it('should set instruction context key to false when disabled', async () => {
        const projector = new ConfigContextProjector(
            createMockConfigManager({
                instructions: { 'code-review.instructions.md': { enabled: false } },
            }),
            catalog,
            toolsCatalog,
            outputChannel,
        );
        await projector.project();

        expect(findSetContextCall('autocontext.instructions.codeReview')?.[2]).toBe(false);
        expect.soft(findSetContextCall('autocontext.instructions.lang.csharp')?.[2]).toBe(true);
    });

    it('should set task context key to false when parent disables the task', async () => {
        const projector = new ConfigContextProjector(
            createMockConfigManager({
                mcpTools: { read_editorconfig_properties: { disabledTasks: ['get_editorconfig_rules'] } },
            }),
            catalog,
            toolsCatalog,
            outputChannel,
        );
        await projector.project();

        expect(findSetContextCall('autocontext.mcpTools.get_editorconfig_rules')?.[2]).toBe(false);
        expect.soft(findSetContextCall('autocontext.mcpTools.analyze_csharp_coding_style')?.[2]).toBe(true);
    });

    it('should set task context key to false when in disabledTasks list', async () => {
        const projector = new ConfigContextProjector(
            createMockConfigManager({
                mcpTools: { analyze_csharp_code: { disabledTasks: ['analyze_csharp_coding_style'] } },
            }),
            catalog,
            toolsCatalog,
            outputChannel,
        );
        await projector.project();

        expect(findSetContextCall('autocontext.mcpTools.analyze_csharp_coding_style')?.[2]).toBe(false);
        expect.soft(findSetContextCall('autocontext.mcpTools.analyze_csharp_async_patterns')?.[2]).toBe(true);
    });

    it('should disable all tasks when parent has enabled false', async () => {
        const projector = new ConfigContextProjector(
            createMockConfigManager({
                mcpTools: {
                    analyze_csharp_code: {
                        enabled: false,
                        disabledTasks: ['analyze_csharp_coding_style', 'analyze_csharp_async_patterns'],
                    },
                },
            }),
            catalog,
            toolsCatalog,
            outputChannel,
        );
        await projector.project();

        expect(findSetContextCall('autocontext.mcpTools.analyze_csharp_coding_style')?.[2]).toBe(false);
        expect.soft(findSetContextCall('autocontext.mcpTools.analyze_csharp_async_patterns')?.[2]).toBe(false);
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
            outputChannel,
        );
        await projector.project();

        expect.soft(findSetContextCall('autocontext.instructions.codeReview')?.[2]).toBe(true);
    });

    it('should log to outputChannel when project fails via onDidChange', async () => {
        let onDidChangeCallback!: () => void;
        const failingManager = {
            read: vi.fn().mockRejectedValue(new Error('read boom')),
            onDidChange: vi.fn((cb: () => void) => { onDidChangeCallback = cb; return { dispose: vi.fn() }; }),
        } as unknown as import('../../src/autocontext-config').AutoContextConfigManager;

        const oc = createFakeOutputChannel();
        const _projector = new ConfigContextProjector(failingManager, catalog, toolsCatalog, oc);

        onDidChangeCallback();
        await vi.waitFor(() => {
            expect(oc.appendLine).toHaveBeenCalledWith(
                expect.stringContaining('[ConfigProjector] Failed to project config: read boom'),
            );
        });

        _projector.dispose();
    });
});

describe('isToolEnabled', () => {
    it('should return true when no tools config exists', () => {
        expect.soft(isToolEnabled({}, 'analyze_csharp_code', 'analyze_csharp_coding_style')).toBe(true);
    });

    it('should return false for standalone tool set to false', () => {
        expect.soft(isToolEnabled(
            { mcpTools: { get_editorconfig_rules: false } }, 'get_editorconfig_rules',
        )).toBe(false);
    });

    it('should return false for tool with enabled false', () => {
        expect.soft(isToolEnabled(
            { mcpTools: { get_editorconfig_rules: { enabled: false } } }, 'get_editorconfig_rules',
        )).toBe(false);
    });

    it('should return false for task in disabled list even when parent is also disabled', () => {
        expect.soft(isToolEnabled(
            { mcpTools: { analyze_csharp_code: { enabled: false, disabledTasks: ['analyze_csharp_coding_style'] } } },
            'analyze_csharp_code', 'analyze_csharp_coding_style',
        )).toBe(false);
    });

    it('should return true for task not in disabled list even when parent is disabled', () => {
        expect.soft(isToolEnabled(
            { mcpTools: { analyze_csharp_code: { enabled: false, disabledTasks: ['analyze_csharp_coding_style'] } } },
            'analyze_csharp_code', 'analyze_csharp_async_patterns',
        )).toBe(true);
    });

    it('should return false for task in disabled list', () => {
        expect.soft(isToolEnabled(
            { mcpTools: { analyze_csharp_code: { disabledTasks: ['analyze_csharp_coding_style'] } } },
            'analyze_csharp_code', 'analyze_csharp_coding_style',
        )).toBe(false);
    });

    it('should return true for task not in disabled list', () => {
        expect.soft(isToolEnabled(
            { mcpTools: { analyze_csharp_code: { disabledTasks: ['analyze_csharp_async_patterns'] } } },
            'analyze_csharp_code', 'analyze_csharp_coding_style',
        )).toBe(true);
    });

    it('should return true for task when parent is shorthand false', () => {
        expect.soft(isToolEnabled(
            { mcpTools: { analyze_csharp_code: false } }, 'analyze_csharp_code', 'analyze_csharp_coding_style',
        )).toBe(true);
    });

    it('should return true for standalone tool with no config entry', () => {
        expect.soft(isToolEnabled(
            { mcpTools: { analyze_csharp_code: false } }, 'get_editorconfig_rules',
        )).toBe(true);
    });
});
