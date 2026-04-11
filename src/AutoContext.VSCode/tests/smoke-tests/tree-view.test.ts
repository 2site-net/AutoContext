import * as assert from 'node:assert/strict';
import * as vscode from 'vscode';
import { activatedExtension } from './helpers.js';

suite('Instructions Tree View Smoke Tests', () => {
    test('tree view should return root categories', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.instructionsTreeProvider.getChildren();

        assert.ok(roots.length > 0, 'Tree view should return at least one root category');
        assert.ok(
            roots.every((r: { kind: string }) => r.kind === 'category'),
            'All root elements should be category nodes',
        );
    });

    test('tree view should return instructions under each category', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.instructionsTreeProvider.getChildren();

        for (const root of roots) {
            const children = exports.instructionsTreeProvider.getChildren(root);
            assert.ok(children.length > 0, `Category '${root.name}' should have at least one instruction`);
            assert.ok(
                children.every((c: { kind: string }) => c.kind === 'instructions'),
                `All children of '${root.name}' should be instruction nodes`,
            );
        }
    });

    test('tree items should have labels and icons', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.instructionsTreeProvider.getChildren();
        const general = roots.find((r: { kind: string; name: string }) => r.kind === 'category' && r.name === 'General');
        assert.ok(general, 'General category should exist');

        const children = exports.instructionsTreeProvider.getChildren(general);
        for (const child of children) {
            const item = exports.instructionsTreeProvider.getTreeItem(child);
            assert.ok(item.label, 'Tree item should have a label');
            assert.ok(item.iconPath, 'Tree item should have an icon');
        }
    });

    test('instruction items should have a command to open virtual document', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.instructionsTreeProvider.getChildren();
        const general = roots.find((r: { kind: string; name: string }) => r.kind === 'category' && r.name === 'General');
        const children = exports.instructionsTreeProvider.getChildren(general);
        const item = exports.instructionsTreeProvider.getTreeItem(children[0]);

        assert.ok(item.command, 'Instruction item should have a command');
        assert.strictEqual(item.command.command, 'vscode.open', 'Command should be vscode.open');
        assert.strictEqual(item.command.arguments[0].scheme, 'sharppilot-instructions', 'URI scheme should be sharppilot-instructions');
    });

    test('entering export mode should show checkboxes on active items', async () => {
        const { exports } = await activatedExtension();

        exports.instructionsTreeProvider.enterExportMode();

        const roots = exports.instructionsTreeProvider.getChildren();
        const general = roots.find((r: { kind: string; name: string }) => r.kind === 'category' && r.name === 'General');
        const children = exports.instructionsTreeProvider.getChildren(general);
        const active = children.find((c: { state: string }) => c.state === 'active');

        if (active) {
            const item = exports.instructionsTreeProvider.getTreeItem(active);
            assert.strictEqual(item.checkboxState, vscode.TreeItemCheckboxState.Unchecked, 'Active items should have unchecked checkbox in export mode');
        }

        exports.instructionsTreeProvider.cancelExportMode();
    });

    test('canceling export mode should remove checkboxes', async () => {
        const { exports } = await activatedExtension();

        exports.instructionsTreeProvider.enterExportMode();
        exports.instructionsTreeProvider.cancelExportMode();

        const roots = exports.instructionsTreeProvider.getChildren();
        const general = roots.find((r: { kind: string; name: string }) => r.kind === 'category' && r.name === 'General');
        const children = exports.instructionsTreeProvider.getChildren(general);

        for (const child of children) {
            const item = exports.instructionsTreeProvider.getTreeItem(child);
            assert.strictEqual(item.checkboxState, undefined, 'Checkbox should not be present outside export mode');
        }
    });

    test('not-detected items should not have checkboxes in export mode', async () => {
        const { exports } = await activatedExtension();

        exports.instructionsTreeProvider.enterExportMode();

        const roots = exports.instructionsTreeProvider.getChildren();
        for (const root of roots) {
            const children = exports.instructionsTreeProvider.getChildren(root);
            const notDetected = children.filter((c: { state: string }) => c.state === 'notDetected');

            for (const nd of notDetected) {
                const item = exports.instructionsTreeProvider.getTreeItem(nd);
                assert.strictEqual(item.checkboxState, undefined, `Not-detected item '${item.label}' should not have a checkbox`);
            }
        }

        exports.instructionsTreeProvider.cancelExportMode();
    });

    test('enable command should update setting to true', async () => {
        const { exports } = await activatedExtension();

        await vscode.workspace.getConfiguration().update('sharppilot.instructions.designPrinciples', false, vscode.ConfigurationTarget.Global);

        try {
            const roots = exports.instructionsTreeProvider.getChildren();
            const general = roots.find((r: { kind: string; name: string }) => r.kind === 'category' && r.name === 'General');
            const children = exports.instructionsTreeProvider.getChildren(general);
            const disabled = children.find(
                (c: { kind: string; entry: { settingId: string }; state: string }) =>
                    c.kind === 'instructions' && c.entry.settingId === 'sharppilot.instructions.designPrinciples',
            );

            assert.ok(disabled, 'Design Principles should be found');
            assert.strictEqual(disabled.state, 'disabled', 'Should be disabled');

            await vscode.commands.executeCommand('sharppilot.enable-instruction', disabled);

            const value = vscode.workspace.getConfiguration().get<boolean>('sharppilot.instructions.designPrinciples');
            assert.strictEqual(value, true, 'Setting should be true after enable');
        } finally {
            await vscode.workspace.getConfiguration().update('sharppilot.instructions.designPrinciples', undefined, vscode.ConfigurationTarget.Global);
        }
    });

    test('disable command should update setting to false', async () => {
        const { exports } = await activatedExtension();

        const roots = exports.instructionsTreeProvider.getChildren();
        const general = roots.find((r: { kind: string; name: string }) => r.kind === 'category' && r.name === 'General');
        const children = exports.instructionsTreeProvider.getChildren(general);
        const active = children.find((c: { kind: string; state: string }) => c.kind === 'instructions' && c.state === 'active');

        assert.ok(active, 'Should have at least one active instruction');

        try {
            await vscode.commands.executeCommand('sharppilot.disable-instruction', active);

            const value = vscode.workspace.getConfiguration().get<boolean>(active.entry.settingId);
            assert.strictEqual(value, false, 'Setting should be false after disable');
        } finally {
            await vscode.workspace.getConfiguration().update(active.entry.settingId, undefined, vscode.ConfigurationTarget.Global);
        }
    });
});
