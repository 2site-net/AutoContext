import { describe, it, expect, vi, beforeEach } from 'vitest';
import { window, __setConfigStore } from './__mocks__/vscode';

import { ToolsToggler } from '../src/tools-toggler';
import { tools } from '../src/config';

beforeEach(() => {
    vi.clearAllMocks();
    __setConfigStore({});
});

describe('ToolsToggler', () => {
    it('should show a multi-select QuickPick with all tools', async () => {
        vi.mocked(window.showQuickPick).mockResolvedValue(undefined);

        const toggler = new ToolsToggler();
        await toggler.toggle();

        expect(window.showQuickPick).toHaveBeenCalledOnce();

        const [items, options] = vi.mocked(window.showQuickPick).mock.calls[0];
        expect(options).toMatchObject({ canPickMany: true, title: 'SharpPilot: Toggle Tools' });
        expect(items).toHaveLength(tools.length);
    });

    it('should not update settings when the user cancels', async () => {
        vi.mocked(window.showQuickPick).mockResolvedValue(undefined);

        const toggler = new ToolsToggler();
        await toggler.toggle();

        const config = (await import('./__mocks__/vscode')).workspace.getConfiguration();
        expect(config.update).not.toHaveBeenCalled();
    });
});
