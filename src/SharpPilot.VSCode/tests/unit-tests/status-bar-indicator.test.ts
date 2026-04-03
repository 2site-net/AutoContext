import { describe, it, expect, beforeEach, vi } from 'vitest';
import { __setConfigStore, window } from './__mocks__/vscode';
import { StatusBarIndicator } from '../../src/status-bar-indicator';
import { InstructionsRegistry } from '../../src/instructions-registry';
import { McpToolsRegistry } from '../../src/mcp-tools-registry';

beforeEach(() => {
    vi.clearAllMocks();
    __setConfigStore({});
});

describe('StatusBarIndicator', () => {
    it('should show all items enabled by default', () => {
        const indicator = new StatusBarIndicator();
        const item = vi.mocked(window.createStatusBarItem).mock.results[0].value;

        expect(item.text).toBe(`$(book) ${InstructionsRegistry.count}/${InstructionsRegistry.count} $(tools) ${McpToolsRegistry.count}/${McpToolsRegistry.count}`);

        indicator.dispose();
    });

    it('should reflect disabled items after update', () => {
        __setConfigStore({
            'sharppilot.instructions.codeReview': false,
            'sharppilot.tools.check_csharp_coding_style': false,
        });

        const indicator = new StatusBarIndicator();
        const item = vi.mocked(window.createStatusBarItem).mock.results[0].value;

        expect(item.text).toBe(`$(book) ${InstructionsRegistry.count - 1}/${InstructionsRegistry.count} $(tools) ${McpToolsRegistry.count - 1}/${McpToolsRegistry.count}`);

        indicator.dispose();
    });

    it('should register the toggle menu command on the status bar item', () => {
        const indicator = new StatusBarIndicator();
        const item = vi.mocked(window.createStatusBarItem).mock.results[0].value;

        expect(item.command).toBe(StatusBarIndicator.commandId);

        indicator.dispose();
    });
});
