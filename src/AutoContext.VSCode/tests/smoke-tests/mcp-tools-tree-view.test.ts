import * as assert from 'node:assert/strict';
import * as vscode from 'vscode';
import { activatedExtension } from './helpers.js';

suite('MCP Tools Tree View Smoke Tests', () => {
    test('tree view should return root groups', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();

        assert.ok(roots.length > 0, 'Should return at least one root group');
        assert.ok(
            roots.every((r: { kind: string }) => r.kind === 'group'),
            'All root elements should be group nodes',
        );
    });

    test('groups should contain categories', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();

        for (const group of roots) {
            const categories = exports.mcpToolsTreeProvider.getChildren(group);
            assert.ok(categories.length > 0, `Group '${group.name}' should have at least one category`);
            assert.ok(
                categories.every((c: { kind: string }) => c.kind === 'category'),
                `All children of group '${group.name}' should be category nodes`,
            );
        }
    });

    test('categories should contain tools', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();

        for (const group of roots) {
            const categories = exports.mcpToolsTreeProvider.getChildren(group);
            for (const category of categories) {
                const tools = exports.mcpToolsTreeProvider.getChildren(category);
                assert.ok(tools.length > 0, `Category '${category.name}' should have at least one tool`);
                assert.ok(
                    tools.every((t: { kind: string }) => t.kind === 'mcpTool'),
                    `All children of '${category.name}' should be mcpTool nodes`,
                );
            }
        }
    });

    test('composite tools should have feature children', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();
        let compositeCount = 0;

        for (const group of roots) {
            for (const category of exports.mcpToolsTreeProvider.getChildren(group)) {
                for (const tool of exports.mcpToolsTreeProvider.getChildren(category)) {
                    if (tool.features && tool.features.length > 0) {
                        compositeCount++;
                        const features = exports.mcpToolsTreeProvider.getChildren(tool);
                        assert.ok(features.length > 0, `Composite tool '${tool.toolName}' should have visible features`);
                        assert.ok(
                            features.every((f: { kind: string }) => f.kind === 'mcpToolFeature'),
                            `All children of '${tool.toolName}' should be mcpToolFeature nodes`,
                        );
                    }
                }
            }
        }

        assert.ok(compositeCount > 0, 'Should have at least one composite tool with features');
    });

    test('tree items should have labels', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();

        for (const group of roots) {
            const groupItem = exports.mcpToolsTreeProvider.getTreeItem(group);
            assert.ok(groupItem.label, `Group item should have a label`);

            for (const category of exports.mcpToolsTreeProvider.getChildren(group)) {
                const catItem = exports.mcpToolsTreeProvider.getTreeItem(category);
                assert.ok(catItem.label, `Category item should have a label`);
            }
        }
    });

    test('detected feature items should have checkboxes', async () => {
        const { exports } = await activatedExtension();
        await exports.workspaceContextDetector.detect();

        const roots = exports.mcpToolsTreeProvider.getChildren();
        let checked = 0;

        for (const group of roots) {
            for (const category of exports.mcpToolsTreeProvider.getChildren(group)) {
                for (const tool of exports.mcpToolsTreeProvider.getChildren(category)) {
                    const features = exports.mcpToolsTreeProvider.getChildren(tool);
                    for (const feature of features) {
                        const item = exports.mcpToolsTreeProvider.getTreeItem(feature);
                        if (feature.state.value !== 'notDetected') {
                            assert.ok(
                                item.checkboxState === vscode.TreeItemCheckboxState.Checked
                                || item.checkboxState === vscode.TreeItemCheckboxState.Unchecked,
                                `Detected feature '${item.label}' should have a checkbox`,
                            );
                            checked++;
                        }
                    }
                }
            }
        }

        assert.ok(checked > 0, 'Should have at least one detected feature with a checkbox');
    });

    test('not-detected items should show description and icon', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();
        let notDetectedCount = 0;

        for (const group of roots) {
            for (const category of exports.mcpToolsTreeProvider.getChildren(group)) {
                for (const tool of exports.mcpToolsTreeProvider.getChildren(category)) {
                    // Leaf tools (no features)
                    if ((!tool.features || tool.features.length === 0) && tool.leafState?.value === 'notDetected') {
                        const item = exports.mcpToolsTreeProvider.getTreeItem(tool);
                        assert.ok(item.description, `Not-detected leaf tool '${tool.toolName}' should have a description`);
                        assert.ok(item.iconPath, `Not-detected leaf tool '${tool.toolName}' should have an icon`);
                        notDetectedCount++;
                    }
                    // Features
                    for (const feature of exports.mcpToolsTreeProvider.getChildren(tool)) {
                        if (feature.state.value === 'notDetected') {
                            const item = exports.mcpToolsTreeProvider.getTreeItem(feature);
                            assert.ok(item.description, `Not-detected feature '${feature.entry.label}' should have a description`);
                            assert.ok(item.iconPath, `Not-detected feature '${feature.entry.label}' should have an icon`);
                            notDetectedCount++;
                        }
                    }
                }
            }
        }

        // This workspace has TypeScript but not .NET, so some tools should be not-detected
        assert.ok(notDetectedCount > 0, 'Should have at least one not-detected item');
    });

    test('all tree items should have tooltips', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();

        for (const group of roots) {
            const groupItem = exports.mcpToolsTreeProvider.getTreeItem(group);
            assert.ok(groupItem.tooltip, `Group '${group.name}' should have a tooltip`);

            for (const category of exports.mcpToolsTreeProvider.getChildren(group)) {
                const catItem = exports.mcpToolsTreeProvider.getTreeItem(category);
                assert.ok(catItem.tooltip, `Category '${category.name}' should have a tooltip`);

                for (const tool of exports.mcpToolsTreeProvider.getChildren(category)) {
                    const toolItem = exports.mcpToolsTreeProvider.getTreeItem(tool);
                    assert.ok(toolItem.tooltip, `Tool '${tool.toolName}' should have a tooltip`);

                    for (const feature of exports.mcpToolsTreeProvider.getChildren(tool)) {
                        const featureItem = exports.mcpToolsTreeProvider.getTreeItem(feature);
                        assert.ok(featureItem.tooltip, `Feature '${feature.entry.label}' should have a tooltip`);
                    }
                }
            }
        }
    });

    test('feature tooltips should contain setting ID', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();

        for (const group of roots) {
            for (const category of exports.mcpToolsTreeProvider.getChildren(group)) {
                for (const tool of exports.mcpToolsTreeProvider.getChildren(category)) {
                    for (const feature of exports.mcpToolsTreeProvider.getChildren(tool)) {
                        const item = exports.mcpToolsTreeProvider.getTreeItem(feature);
                        assert.ok(
                            (item.tooltip as string).includes('Setting:'),
                            `Feature tooltip should contain 'Setting:' prefix`,
                        );
                        assert.ok(
                            (item.tooltip as string).includes(feature.entry.settingId),
                            `Feature tooltip should contain setting ID '${feature.entry.settingId}'`,
                        );
                    }
                }
            }
        }
    });
});
