import { describe, it, expect } from 'vitest';

import { AutoContextConfig } from '#src/autocontext-config';

describe('AutoContextConfig.isToolEnabled', () => {
    it('should return true when no tools config exists', () => {
        expect.soft(new AutoContextConfig().isToolEnabled('analyze_csharp_code', 'analyze_csharp_coding_style')).toBe(true);
    });

    it('should return false for standalone tool set to false', () => {
        expect.soft(new AutoContextConfig(
            { mcpTools: { get_editorconfig_rules: false } }).isToolEnabled('get_editorconfig_rules',
        )).toBe(false);
    });

    it('should return false for tool with enabled false', () => {
        expect.soft(new AutoContextConfig(
            { mcpTools: { get_editorconfig_rules: { enabled: false } } }).isToolEnabled('get_editorconfig_rules',
        )).toBe(false);
    });

    it('should return false for task in disabled list even when parent is also disabled', () => {
        expect.soft(new AutoContextConfig(
            { mcpTools: { analyze_csharp_code: { enabled: false, disabledTasks: ['analyze_csharp_coding_style'] } } }).isToolEnabled(
            'analyze_csharp_code', 'analyze_csharp_coding_style',
        )).toBe(false);
    });

    it('should return true for task not in disabled list even when parent is disabled', () => {
        expect.soft(new AutoContextConfig(
            { mcpTools: { analyze_csharp_code: { enabled: false, disabledTasks: ['analyze_csharp_coding_style'] } } }).isToolEnabled(
            'analyze_csharp_code', 'analyze_csharp_async_patterns',
        )).toBe(true);
    });

    it('should return false for task in disabled list', () => {
        expect.soft(new AutoContextConfig(
            { mcpTools: { analyze_csharp_code: { disabledTasks: ['analyze_csharp_coding_style'] } } }).isToolEnabled(
            'analyze_csharp_code', 'analyze_csharp_coding_style',
        )).toBe(false);
    });

    it('should return true for task not in disabled list', () => {
        expect.soft(new AutoContextConfig(
            { mcpTools: { analyze_csharp_code: { disabledTasks: ['analyze_csharp_async_patterns'] } } }).isToolEnabled(
            'analyze_csharp_code', 'analyze_csharp_coding_style',
        )).toBe(true);
    });

    it('should return true for task when parent is shorthand false', () => {
        expect.soft(new AutoContextConfig(
            { mcpTools: { analyze_csharp_code: false } }).isToolEnabled('analyze_csharp_code', 'analyze_csharp_coding_style',
        )).toBe(true);
    });

    it('should return true for standalone tool with no config entry', () => {
        expect.soft(new AutoContextConfig(
            { mcpTools: { analyze_csharp_code: false } }).isToolEnabled('get_editorconfig_rules',
        )).toBe(true);
    });
});

describe('AutoContextConfig.projectDisabledState', () => {
    it('returns empty lists when mcpTools is missing', () => {
        const snapshot = new AutoContextConfig().getToolsDisabledSnapshot();
        expect(snapshot.disabledTools).toEqual([]);
        expect(snapshot.disabledTasks).toEqual({});
    });

    it('treats shorthand false as a disabled tool with no disabled tasks', () => {
        const snapshot = new AutoContextConfig({ mcpTools: { analyze_csharp_code: false } }).getToolsDisabledSnapshot();
        expect(snapshot.disabledTools).toEqual(['analyze_csharp_code']);
        expect(snapshot.disabledTasks).toEqual({});
    });

    it('treats { enabled: false } as a disabled tool', () => {
        const snapshot = new AutoContextConfig({
            mcpTools: { analyze_csharp_code: { enabled: false } },
        }).getToolsDisabledSnapshot();
        expect(snapshot.disabledTools).toEqual(['analyze_csharp_code']);
        expect(snapshot.disabledTasks).toEqual({});
    });

    it('records disabledTasks even when the parent tool is enabled', () => {
        const snapshot = new AutoContextConfig({
            mcpTools: { analyze_csharp_code: { disabledTasks: ['analyze_csharp_coding_style'] } },
        }).getToolsDisabledSnapshot();
        expect(snapshot.disabledTools).toEqual([]);
        expect(snapshot.disabledTasks).toEqual({
            analyze_csharp_code: ['analyze_csharp_coding_style'],
        });
    });

    it('records both the disabled parent and disabled tasks when both are set', () => {
        const snapshot = new AutoContextConfig({
            mcpTools: {
                analyze_csharp_code: {
                    enabled: false,
                    disabledTasks: ['analyze_csharp_coding_style', 'analyze_csharp_async_patterns'],
                },
            },
        }).getToolsDisabledSnapshot();
        expect(snapshot.disabledTools).toEqual(['analyze_csharp_code']);
        expect(snapshot.disabledTasks).toEqual({
            analyze_csharp_code: ['analyze_csharp_coding_style', 'analyze_csharp_async_patterns'],
        });
    });

    it('skips empty disabledTasks arrays', () => {
        const snapshot = new AutoContextConfig({
            mcpTools: { analyze_csharp_code: { disabledTasks: [] } },
        }).getToolsDisabledSnapshot();
        expect(snapshot.disabledTasks).toEqual({});
    });

    it('omits enabled tools that have no disabled tasks', () => {
        const snapshot = new AutoContextConfig({
            mcpTools: { analyze_csharp_code: { version: '1.0.0' } },
        }).getToolsDisabledSnapshot();
        expect(snapshot.disabledTools).toEqual([]);
        expect(snapshot.disabledTasks).toEqual({});
    });

    it('does not share disabledTasks references with the source config', () => {
        const tasks = ['analyze_csharp_coding_style'];
        const snapshot = new AutoContextConfig({
            mcpTools: { analyze_csharp_code: { disabledTasks: tasks } },
        }).getToolsDisabledSnapshot();
        tasks.push('mutated');
        expect(snapshot.disabledTasks['analyze_csharp_code']).toEqual(['analyze_csharp_coding_style']);
    });
});
