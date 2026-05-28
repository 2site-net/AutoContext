import * as assert from 'node:assert/strict';
import * as vscode from 'vscode';
import { activatedExtension } from './helpers.js';

suite('MCP Tools Tree View Smoke Tests', () => {
    test('should return root nodes from the tree view', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();

        assert.ok(roots.length > 0, 'Should return at least one root node');
        assert.equal(roots[0].kind, 'mcpServerNode', 'First root should be the MCP server status row');
        assert.ok(
            roots.slice(1).every((r: { kind: string }) => r.kind === 'mcpTopCategoryNode'),
            'Roots after the server status row should all be mcpTopCategoryNode nodes',
        );
    });

    test('should contain sub-categories under top categories', async () => {
        const { exports } = await activatedExtension();
        const topCategories = exports.mcpToolsTreeProvider.getChildren()
            .filter((r: { kind: string }) => r.kind === 'mcpTopCategoryNode');

        assert.ok(topCategories.length > 0, 'Should return at least one top category');

        for (const topCategory of topCategories) {
            const subCategories = exports.mcpToolsTreeProvider.getChildren(topCategory);
            assert.ok(subCategories.length > 0, `Top category '${topCategory.name}' should have at least one sub-category`);
            assert.ok(
                subCategories.every((c: { kind: string }) => c.kind === 'mcpSubCategoryNode'),
                `All children of top category '${topCategory.name}' should be mcpSubCategoryNode nodes`,
            );
        }
    });

    test('should contain tools under sub-categories', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();

        for (const topCategory of roots) {
            const subCategories = exports.mcpToolsTreeProvider.getChildren(topCategory);
            for (const subCategory of subCategories) {
                const tools = exports.mcpToolsTreeProvider.getChildren(subCategory);
                assert.ok(tools.length > 0, `Sub-category '${subCategory.name}' should have at least one tool`);
                assert.ok(
                    tools.every((t: { kind: string }) => t.kind === 'mcpToolNode'),
                    `All children of '${subCategory.name}' should be mcpToolNode nodes`,
                );
            }
        }
    });

    test('should expose task children under composite tools', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();
        let compositeCount = 0;

        for (const topCategory of roots) {
            for (const subCategory of exports.mcpToolsTreeProvider.getChildren(topCategory)) {
                for (const tool of exports.mcpToolsTreeProvider.getChildren(subCategory)) {
                    if (tool.tasks && tool.tasks.length > 0) {
                        compositeCount++;
                        const tasks = exports.mcpToolsTreeProvider.getChildren(tool);
                        assert.ok(tasks.length > 0, `Composite tool '${tool.toolName}' should have visible tasks`);
                        assert.ok(
                            tasks.every((f: { kind: string }) => f.kind === 'mcpTaskNode'),
                            `All children of '${tool.toolName}' should be mcpTaskNode nodes`,
                        );
                    }
                }
            }
        }

        assert.ok(compositeCount > 0, 'Should have at least one composite tool with tasks');
    });

    test('should expose labels on tree items', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();

        for (const topCategory of roots) {
            const topCatItem = exports.mcpToolsTreeProvider.getTreeItem(topCategory);
            assert.ok(topCatItem.label, `Top category item should have a label`);

            for (const subCategory of exports.mcpToolsTreeProvider.getChildren(topCategory)) {
                const subCatItem = exports.mcpToolsTreeProvider.getTreeItem(subCategory);
                assert.ok(subCatItem.label, `Sub-category item should have a label`);
            }
        }
    });

    test('should expose checkboxes on detected task items', async () => {
        const { exports } = await activatedExtension();
        await exports.workspaceContextDetector.detect();

        const roots = exports.mcpToolsTreeProvider.getChildren();
        let checked = 0;

        for (const topCategory of roots) {
            for (const subCategory of exports.mcpToolsTreeProvider.getChildren(topCategory)) {
                for (const tool of exports.mcpToolsTreeProvider.getChildren(subCategory)) {
                    const tasks = exports.mcpToolsTreeProvider.getChildren(tool);
                    for (const task of tasks) {
                        const item = exports.mcpToolsTreeProvider.getTreeItem(task);
                        if (task.state.value !== 'notDetected') {
                            assert.ok(
                                item.checkboxState === vscode.TreeItemCheckboxState.Checked
                                || item.checkboxState === vscode.TreeItemCheckboxState.Unchecked,
                                `Detected task '${item.label}' should have a checkbox`,
                            );
                            checked++;
                        }
                    }
                }
            }
        }

        assert.ok(checked > 0, 'Should have at least one detected task with a checkbox');
    });

    test('should show description and icon on not-detected items', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();
        let notDetectedCount = 0;

        for (const topCategory of roots) {
            for (const subCategory of exports.mcpToolsTreeProvider.getChildren(topCategory)) {
                for (const tool of exports.mcpToolsTreeProvider.getChildren(subCategory)) {
                    // Leaf tools (single task whose name matches the tool name)
                    if (tool.isLeaf && tool.tasks[0]?.state.value === 'notDetected') {
                        const item = exports.mcpToolsTreeProvider.getTreeItem(tool);
                        assert.ok(item.description, `Not-detected leaf tool '${tool.tool.name}' should have a description`);
                        assert.ok(item.iconPath, `Not-detected leaf tool '${tool.tool.name}' should have an icon`);
                        notDetectedCount++;
                    }
                    // Tasks
                    for (const task of exports.mcpToolsTreeProvider.getChildren(tool)) {
                        if (task.state.value === 'notDetected') {
                            const item = exports.mcpToolsTreeProvider.getTreeItem(task);
                            assert.ok(item.description, `Not-detected task '${task.task.name}' should have a description`);
                            assert.ok(item.iconPath, `Not-detected task '${task.task.name}' should have an icon`);
                            notDetectedCount++;
                        }
                    }
                }
            }
        }

        // This workspace has TypeScript but not .NET, so some tools should be not-detected
        assert.ok(notDetectedCount > 0, 'Should have at least one not-detected item');
    });

    test('should expose tooltips on all tree items', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();

        for (const topCategory of roots) {
            const topCatItem = exports.mcpToolsTreeProvider.getTreeItem(topCategory);
            assert.ok(topCatItem.tooltip, `Top category '${topCategory.name}' should have a tooltip`);

            for (const subCategory of exports.mcpToolsTreeProvider.getChildren(topCategory)) {
                const subCatItem = exports.mcpToolsTreeProvider.getTreeItem(subCategory);
                assert.ok(subCatItem.tooltip, `Sub-category '${subCategory.name}' should have a tooltip`);

                for (const tool of exports.mcpToolsTreeProvider.getChildren(subCategory)) {
                    const toolItem = exports.mcpToolsTreeProvider.getTreeItem(tool);
                    assert.ok(toolItem.tooltip, `Tool '${tool.toolName}' should have a tooltip`);

                    for (const task of exports.mcpToolsTreeProvider.getChildren(tool)) {
                        const taskItem = exports.mcpToolsTreeProvider.getTreeItem(task);
                        assert.ok(taskItem.tooltip, `task '${task.task.name}' should have a tooltip`);
                    }
                }
            }
        }
    });

    test('should contain the setting ID in task tooltips', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();

        for (const topCategory of roots) {
            for (const subCategory of exports.mcpToolsTreeProvider.getChildren(topCategory)) {
                for (const tool of exports.mcpToolsTreeProvider.getChildren(subCategory)) {
                    for (const task of exports.mcpToolsTreeProvider.getChildren(tool)) {
                        const item = exports.mcpToolsTreeProvider.getTreeItem(task);
                        assert.ok(
                            (item.tooltip as string).includes('Context Key:'),
                            `task tooltip should contain 'Context Key:' prefix`,
                        );
                        assert.ok(
                            (item.tooltip as string).includes(task.task.runtimeInfo.contextKey),
                            `task tooltip should contain context key '${task.task.runtimeInfo.contextKey}'`,
                        );
                    }
                }
            }
        }
    });
});
