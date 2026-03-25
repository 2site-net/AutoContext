import { describe, it, expect, beforeEach, vi } from 'vitest';
import { __setConfigStore, window } from './__mocks__/vscode';
import { StatusBarIndicator } from '../../src/status-bar-indicator';
import { instructions, tools } from '../../src/config';

beforeEach(() => {
    vi.clearAllMocks();
    __setConfigStore({});
});

describe('StatusBarIndicator', () => {
    it('should show all items enabled by default', () => {
        const indicator = new StatusBarIndicator();
        const item = vi.mocked(window.createStatusBarItem).mock.results[0].value;

        expect(item.text).toBe(`$(book) ${instructions.length}/${instructions.length} $(tools) ${tools.length}/${tools.length}`);

        indicator.dispose();
    });

    it('should reflect disabled items after update', () => {
        __setConfigStore({
            'sharppilot.instructions.codeReview': false,
            'sharppilot.tools.check_csharp_coding_style': false,
        });

        const indicator = new StatusBarIndicator();
        const item = vi.mocked(window.createStatusBarItem).mock.results[0].value;

        expect(item.text).toBe(`$(book) ${instructions.length - 1}/${instructions.length} $(tools) ${tools.length - 1}/${tools.length}`);

        indicator.dispose();
    });

    it('should register the toggle menu command on the status bar item', () => {
        const indicator = new StatusBarIndicator();
        const item = vi.mocked(window.createStatusBarItem).mock.results[0].value;

        expect(item.command).toBe(StatusBarIndicator.commandId);

        indicator.dispose();
    });
});
