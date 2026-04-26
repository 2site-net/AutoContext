import * as assert from 'node:assert/strict';
import * as vscode from 'vscode';
import { activatedExtension } from './helpers.js';

suite('Instructions Tree View Smoke Tests', () => {
    test('tree view should return root categories', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.instructionsTreeProvider.getChildren();

        assert.ok(roots.length > 0, 'Tree view should return at least one root category');
        assert.ok(
            roots.every((r: { kind: string }) => r.kind === 'categoryNode'),
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
        const general = roots.find((r: { kind: string; name: string }) => r.kind === 'categoryNode' && r.name === 'General');
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
        const general = roots.find((r: { kind: string; name: string }) => r.kind === 'categoryNode' && r.name === 'General');
        const children = exports.instructionsTreeProvider.getChildren(general);
        const item = exports.instructionsTreeProvider.getTreeItem(children[0]);

        assert.ok(item.command, 'Instruction item should have a command');
        assert.strictEqual(item.command.command, 'vscode.open', 'Command should be vscode.open');
        assert.strictEqual(item.command.arguments[0].scheme, 'autocontext-instructions', 'URI scheme should be autocontext-instructions');
    });

    test('entering export mode should show checkboxes on active items', async () => {
        const { exports } = await activatedExtension();

        exports.instructionsTreeProvider.enterExportMode();

        const roots = exports.instructionsTreeProvider.getChildren();
        const general = roots.find((r: { kind: string; name: string }) => r.kind === 'categoryNode' && r.name === 'General');
        const children = exports.instructionsTreeProvider.getChildren(general);
        const active = children.find((c: { state: { value: string } }) => c.state.value === 'enabled');

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
        const general = roots.find((r: { kind: string; name: string }) => r.kind === 'categoryNode' && r.name === 'General');
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
            const notDetected = children.filter((c: { state: { value: string } }) => c.state.value === 'notDetected');

            for (const nd of notDetected) {
                const item = exports.instructionsTreeProvider.getTreeItem(nd);
                assert.strictEqual(item.checkboxState, undefined, `Not-detected item '${item.label}' should not have a checkbox`);
            }
        }

        exports.instructionsTreeProvider.cancelExportMode();
    });

    test('enable command should update config to enabled', async () => {
        const { exports } = await activatedExtension();

        await exports.configManager.setInstructionEnabled('design-principles.instructions.md', false);

        try {
            const roots = exports.instructionsTreeProvider.getChildren();
            const general = roots.find((r: { kind: string; name: string }) => r.kind === 'categoryNode' && r.name === 'General');
            const children = exports.instructionsTreeProvider.getChildren(general);
            const disabled = children.find(
                (c: { kind: string; entry: { name: string }; state: { value: string } }) =>
                    c.kind === 'instructions' && c.entry.name === 'design-principles.instructions.md',
            );

            assert.ok(disabled, 'Design Principles should be found');
            assert.strictEqual(disabled.state.value, 'disabled', 'Should be disabled');

            await vscode.commands.executeCommand('autocontext.enable-instruction', disabled);

            const config = exports.configManager.readSync();
            const entry = config.instructions?.['design-principles.instructions.md'];
            assert.ok(!entry || entry.enabled !== false, 'Instruction should be enabled after enable command');
        } finally {
            await exports.configManager.setInstructionEnabled('design-principles.instructions.md', true);
        }
    });

    test('disable command should update config to disabled', async () => {
        const { exports } = await activatedExtension();

        const roots = exports.instructionsTreeProvider.getChildren();
        const general = roots.find((r: { kind: string; name: string }) => r.kind === 'categoryNode' && r.name === 'General');
        const children = exports.instructionsTreeProvider.getChildren(general);
        const active = children.find((c: { kind: string; state: { value: string } }) => c.kind === 'instructions' && c.state.value === 'enabled');

        assert.ok(active, 'Should have at least one active instruction');

        try {
            await vscode.commands.executeCommand('autocontext.disable-instruction', active);

            const config = exports.configManager.readSync();
            const entry = config.instructions?.[active.entry.name];
            assert.strictEqual(entry?.enabled, false, 'Instruction should be disabled after disable command');
        } finally {
            await exports.configManager.setInstructionEnabled(active.entry.name, true);
        }
    });

    test('all instruction items should have tooltips', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.instructionsTreeProvider.getChildren();

        for (const root of roots) {
            const catItem = exports.instructionsTreeProvider.getTreeItem(root);
            assert.ok(catItem.tooltip, `Category '${root.name}' should have a tooltip`);

            const children = exports.instructionsTreeProvider.getChildren(root);
            for (const child of children) {
                const item = exports.instructionsTreeProvider.getTreeItem(child);
                assert.ok(item.tooltip, `Instruction '${item.label}' should have a tooltip`);
            }
        }
    });

    test('instruction tooltips should contain setting ID', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.instructionsTreeProvider.getChildren();
        const general = roots.find((r: { kind: string; name: string }) => r.kind === 'categoryNode' && r.name === 'General');
        assert.ok(general, 'General category should exist');
        const children = exports.instructionsTreeProvider.getChildren(general);

        for (const child of children) {
            const item = exports.instructionsTreeProvider.getTreeItem(child);
            const tip = item.tooltip as string;
            assert.ok(tip.includes('Context Key:'), `Tooltip should contain 'Context Key:' prefix`);
            assert.ok(tip.includes(child.entry.contextKey), `Tooltip should contain context key '${child.entry.contextKey}'`);
        }
    });

    test('enabled items should have correct context value and icon', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.instructionsTreeProvider.getChildren();
        const general = roots.find((r: { kind: string; name: string }) => r.kind === 'categoryNode' && r.name === 'General');
        const children = exports.instructionsTreeProvider.getChildren(general);
        const enabled = children.find((c: { state: { value: string } }) => c.state.value === 'enabled');

        assert.ok(enabled, 'Should have at least one enabled instruction');
        const item = exports.instructionsTreeProvider.getTreeItem(enabled);
        assert.strictEqual(item.contextValue, 'instruction.enabled', 'Context value should be instruction.enabled');
        assert.ok(item.iconPath, 'Enabled item should have an icon');
    });

    test('not-detected items should have description and icon', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.instructionsTreeProvider.getChildren();
        let notDetectedCount = 0;

        for (const root of roots) {
            const children = exports.instructionsTreeProvider.getChildren(root);
            for (const child of children) {
                if (child.state.value === 'notDetected') {
                    const item = exports.instructionsTreeProvider.getTreeItem(child);
                    assert.strictEqual(item.contextValue, 'instruction.notDetected', 'Context value should be instruction.notDetected');
                    assert.ok(item.description, `Not-detected item '${item.label}' should have a description`);
                    assert.ok(item.iconPath, `Not-detected item '${item.label}' should have an icon`);
                    notDetectedCount++;
                }
            }
        }

        assert.ok(notDetectedCount > 0, 'Should have at least one not-detected instruction');
    });

    test('category tooltips should show active count', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.instructionsTreeProvider.getChildren();
        const general = roots.find((r: { kind: string; name: string }) => r.kind === 'categoryNode' && r.name === 'General');
        assert.ok(general, 'General category should exist');
        const catItem = exports.instructionsTreeProvider.getTreeItem(general);
        const tip = catItem.tooltip as string;

        assert.ok(tip.includes('General'), `Category tooltip should contain category name`);
        assert.match(tip, /\d+\/\d+/, 'Category tooltip should show active/total count');
    });
});
