import { describe, it, expect, vi, beforeEach } from 'vitest';
import { window, __setConfigStore, QuickPickItemKind, type MockQuickPick } from './__mocks__/vscode';

import { MenuToggler } from '../../src/menu-toggler';
import { type CatalogEntry } from '../../src/catalog-entry';
import { InstructionsRegistry } from '../../src/instructions-registry';
import { McpToolsRegistry } from '../../src/mcp-tools-registry';

beforeEach(() => {
    vi.clearAllMocks();
    __setConfigStore({});
});

interface ToggleItem { settingId: string; description?: string; isCategory?: boolean; kind?: number; category?: string; label: string }

function settingItems(qp: MockQuickPick): ToggleItem[] {
    return (qp.items as ToggleItem[]).filter(i => !i.isCategory && i.kind !== QuickPickItemKind.Separator);
}

function categoryHeaders(qp: MockQuickPick): ToggleItem[] {
    return (qp.items as ToggleItem[]).filter(i => i.isCategory);
}

const smallEntries: readonly CatalogEntry[] = [
    { settingId: 'a.one', label: 'One', category: 'Alpha' },
    { settingId: 'a.two', label: 'Two', category: 'Alpha' },
    { settingId: 'b.one', label: 'Three', category: 'Beta' },
];

describe('MenuToggler', () => {
    it('should show a multi-select QuickPick with setting items, separators, and category headers', async () => {
        const toggler = new MenuToggler('SharpPilot: Toggle Tools', 'Select tools to enable', McpToolsRegistry.all);
        const promise = toggler.toggle();

        const qp = vi.mocked(window.createQuickPick).mock.results[0].value as MockQuickPick;
        expect(qp.canSelectMany).toBe(true);
        expect(qp.title).toBe('SharpPilot: Toggle Tools');
        expect(settingItems(qp)).toHaveLength(McpToolsRegistry.count);
        expect(categoryHeaders(qp).length).toBeGreaterThan(0);
        expect(qp.show).toHaveBeenCalledOnce();

        qp.__hide();
        await promise;
    });

    it('should not update settings when the user cancels', async () => {
        const toggler = new MenuToggler('SharpPilot: Toggle Tools', 'Select tools to enable', McpToolsRegistry.all);
        const promise = toggler.toggle();

        const qp = vi.mocked(window.createQuickPick).mock.results[0].value as MockQuickPick;
        qp.__hide();
        await promise;

        const config = (await import('./__mocks__/vscode')).workspace.getConfiguration();
        expect(config.update).not.toHaveBeenCalled();
    });

    it('should have Select All and Clear All buttons', async () => {
        const toggler = new MenuToggler('SharpPilot: Toggle Tools', 'Select tools to enable', McpToolsRegistry.all);
        const promise = toggler.toggle();

        const qp = vi.mocked(window.createQuickPick).mock.results[0].value as MockQuickPick;
        expect(qp.buttons).toHaveLength(2);
        expect(qp.buttons[0]).toMatchObject({ tooltip: 'Select All' });
        expect(qp.buttons[1]).toMatchObject({ tooltip: 'Clear All' });

        qp.__hide();
        await promise;
    });

    it('should append override badge to description for overridden entries', async () => {
        const overriddenId = InstructionsRegistry.all[0].settingId;
        const toggler = new MenuToggler(
            'SharpPilot: Toggle Instructions',
            'Select instructions to enable',
            InstructionsRegistry.all,
            () => new Set([overriddenId]),
        );
        const promise = toggler.toggle();

        const qp = vi.mocked(window.createQuickPick).mock.results[0].value as MockQuickPick;
        const items = qp.items as ToggleItem[];
        const overriddenItem = items.find(i => i.settingId === overriddenId);
        const normalItem = items.find(i => i.settingId === InstructionsRegistry.all[1].settingId);

        expect(overriddenItem?.description).toContain('$(file-symlink-directory)');
        expect(normalItem?.description).not.toContain('$(file-symlink-directory)');

        qp.__hide();
        await promise;
    });

    it('should select all category members when a category header is checked', async () => {
        __setConfigStore({ 'a.one': false, 'a.two': false });
        const toggler = new MenuToggler('Test', 'test', smallEntries);
        const promise = toggler.toggle();

        const qp = vi.mocked(window.createQuickPick).mock.results[0].value as MockQuickPick;
        const items = qp.items as ToggleItem[];
        const alphaHeader = items.find(i => i.isCategory && i.category === 'Alpha')!;
        const betaHeader = items.find(i => i.isCategory && i.category === 'Beta')!;
        const betaItem = items.find(i => i.settingId === 'b.one')!;

        // Simulate checking the Alpha category header (plus keep Beta members selected)
        qp.selectedItems = [alphaHeader, betaHeader, betaItem];

        // Alpha members should now be selected too
        const selectedIds = new Set((qp.selectedItems as ToggleItem[]).map(i => i.settingId));
        expect(selectedIds.has('a.one')).toBe(true);
        expect(selectedIds.has('a.two')).toBe(true);

        qp.__hide();
        await promise;
    });

    it('should deselect all category members when a category header is unchecked', async () => {
        const toggler = new MenuToggler('Test', 'test', smallEntries);
        const promise = toggler.toggle();

        const qp = vi.mocked(window.createQuickPick).mock.results[0].value as MockQuickPick;
        const items = qp.items as ToggleItem[];
        const betaHeader = items.find(i => i.isCategory && i.category === 'Beta')!;
        const betaItem = items.find(i => i.settingId === 'b.one')!;

        // First verify Beta is initially all selected (default true)
        const initialIds = new Set((qp.selectedItems as ToggleItem[]).map(i => i.settingId));
        expect(initialIds.has(betaHeader.settingId)).toBe(true);
        expect(initialIds.has(betaItem.settingId)).toBe(true);

        // Now deselect Beta category header — keep only Alpha items
        const alphaItems = items.filter(i => i.category === 'Alpha' && i.kind !== QuickPickItemKind.Separator);
        qp.selectedItems = [...alphaItems];

        // Beta member should be deselected
        const afterIds = new Set((qp.selectedItems as ToggleItem[]).map(i => i.settingId));
        expect(afterIds.has('b.one')).toBe(false);

        qp.__hide();
        await promise;
    });

    it('should auto-check category header when all members become selected', async () => {
        __setConfigStore({ 'a.one': false });
        const toggler = new MenuToggler('Test', 'test', smallEntries);
        const promise = toggler.toggle();

        const qp = vi.mocked(window.createQuickPick).mock.results[0].value as MockQuickPick;
        const items = qp.items as ToggleItem[];
        const alphaHeader = items.find(i => i.isCategory && i.category === 'Alpha')!;

        // Initially a.one is off, so Alpha header should not be selected
        const initialIds = new Set((qp.selectedItems as ToggleItem[]).map(i => i.settingId));
        expect(initialIds.has(alphaHeader.settingId)).toBe(false);

        // Select both Alpha members manually (without the header)
        const allNonSeparator = items.filter(i => i.kind !== QuickPickItemKind.Separator);
        qp.selectedItems = allNonSeparator.filter(i =>
            i.settingId === 'a.one' || i.settingId === 'a.two' || i.category === 'Beta',
        );

        // Alpha header should now be auto-checked
        const afterIds = new Set((qp.selectedItems as ToggleItem[]).map(i => i.settingId));
        expect(afterIds.has(alphaHeader.settingId)).toBe(true);

        qp.__hide();
        await promise;
    });

    it('should only persist setting entries, not category headers', async () => {
        const toggler = new MenuToggler('Test', 'test', smallEntries);
        const promise = toggler.toggle();

        const qp = vi.mocked(window.createQuickPick).mock.results[0].value as MockQuickPick;
        qp.__accept();
        await promise;

        const config = (await import('./__mocks__/vscode')).workspace.getConfiguration();
        const updatedKeys = (config.update as ReturnType<typeof vi.fn>).mock.calls.map((c: unknown[]) => c[0] as string);
        expect(updatedKeys.every(k => !k.startsWith('__category__'))).toBe(true);
    });
});
