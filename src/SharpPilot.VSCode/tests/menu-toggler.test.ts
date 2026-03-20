import { describe, it, expect, vi, beforeEach } from 'vitest';
import { window, __setConfigStore, type MockQuickPick } from './__mocks__/vscode';

import { MenuToggler } from '../src/menu-toggler';
import { tools } from '../src/config';

beforeEach(() => {
    vi.clearAllMocks();
    __setConfigStore({});
});

describe('MenuToggler', () => {
    it('should show a multi-select QuickPick with all items', async () => {
        const toggler = new MenuToggler('SharpPilot: Toggle Tools', 'Select tools to enable', tools);
        const promise = toggler.toggle();

        const qp = vi.mocked(window.createQuickPick).mock.results[0].value as MockQuickPick;
        expect(qp.canSelectMany).toBe(true);
        expect(qp.title).toBe('SharpPilot: Toggle Tools');
        expect(qp.items).toHaveLength(tools.length);
        expect(qp.show).toHaveBeenCalledOnce();

        qp.__hide();
        await promise;
    });

    it('should not update settings when the user cancels', async () => {
        const toggler = new MenuToggler('SharpPilot: Toggle Tools', 'Select tools to enable', tools);
        const promise = toggler.toggle();

        const qp = vi.mocked(window.createQuickPick).mock.results[0].value as MockQuickPick;
        qp.__hide();
        await promise;

        const config = (await import('./__mocks__/vscode')).workspace.getConfiguration();
        expect(config.update).not.toHaveBeenCalled();
    });

    it('should have Select All and Clear All buttons', async () => {
        const toggler = new MenuToggler('SharpPilot: Toggle Tools', 'Select tools to enable', tools);
        const promise = toggler.toggle();

        const qp = vi.mocked(window.createQuickPick).mock.results[0].value as MockQuickPick;
        expect(qp.buttons).toHaveLength(2);
        expect(qp.buttons[0]).toMatchObject({ tooltip: 'Select All' });
        expect(qp.buttons[1]).toMatchObject({ tooltip: 'Clear All' });

        qp.__hide();
        await promise;
    });
});
