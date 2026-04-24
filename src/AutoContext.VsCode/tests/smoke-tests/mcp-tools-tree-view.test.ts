import * as assert from 'node:assert/strict';
import * as vscode from 'vscode';
import { activatedExtension } from './helpers.js';

suite('MCP Tools Tree View Smoke Tests', () => {
    test('tree view should return root nodes', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();

        assert.ok(roots.length > 0, 'Should return at least one root node');
        assert.ok(
            roots.every((r: { kind: string }) => r.kind === 'serverNode'),
            'All root elements should be serverNode nodes',
        );
    });

    test('server nodes should contain categories', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();

        for (const serverNode of roots) {
            const categories = exports.mcpToolsTreeProvider.getChildren(serverNode);
            assert.ok(categories.length > 0, `Server '${serverNode.name}' should have at least one category`);
            assert.ok(
                categories.every((c: { kind: string }) => c.kind === 'categoryNode'),
                `All children of server '${serverNode.name}' should be categoryNode nodes`,
            );
        }
    });

    test('categories should contain tools', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();

        for (const serverNode of roots) {
            const categories = exports.mcpToolsTreeProvider.getChildren(serverNode);
            for (const category of categories) {
                const tools = exports.mcpToolsTreeProvider.getChildren(category);
                assert.ok(tools.length > 0, `Category '${category.name}' should have at least one tool`);
                assert.ok(
                    tools.every((t: { kind: string }) => t.kind === 'mcpToolNode'),
                    `All children of '${category.name}' should be mcpToolNode nodes`,
                );
            }
        }
    });

    test('composite tools should have task children', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();
        let compositeCount = 0;

        for (const serverNode of roots) {
            for (const category of exports.mcpToolsTreeProvider.getChildren(serverNode)) {
                for (const tool of exports.mcpToolsTreeProvider.getChildren(category)) {
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

    test('tree items should have labels', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();

        for (const serverNode of roots) {
            const serverItem = exports.mcpToolsTreeProvider.getTreeItem(serverNode);
            assert.ok(serverItem.label, `Server item should have a label`);

            for (const category of exports.mcpToolsTreeProvider.getChildren(serverNode)) {
                const catItem = exports.mcpToolsTreeProvider.getTreeItem(category);
                assert.ok(catItem.label, `Category item should have a label`);
            }
        }
    });

    test('detected task items should have checkboxes', async () => {
        const { exports } = await activatedExtension();
        await exports.workspaceContextDetector.detect();

        const roots = exports.mcpToolsTreeProvider.getChildren();
        let checked = 0;

        for (const serverNode of roots) {
            for (const category of exports.mcpToolsTreeProvider.getChildren(serverNode)) {
                for (const tool of exports.mcpToolsTreeProvider.getChildren(category)) {
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

    test('not-detected items should show description and icon', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();
        let notDetectedCount = 0;

        for (const serverNode of roots) {
            for (const category of exports.mcpToolsTreeProvider.getChildren(serverNode)) {
                for (const tool of exports.mcpToolsTreeProvider.getChildren(category)) {
                    // Leaf tools (no tasks)
                    if ((!tool.tasks || tool.tasks.length === 0) && tool.leafState?.value === 'notDetected') {
                        const item = exports.mcpToolsTreeProvider.getTreeItem(tool);
                        assert.ok(item.description, `Not-detected leaf tool '${tool.toolName}' should have a description`);
                        assert.ok(item.iconPath, `Not-detected leaf tool '${tool.toolName}' should have an icon`);
                        notDetectedCount++;
                    }
                    // Tasks
                    for (const task of exports.mcpToolsTreeProvider.getChildren(tool)) {
                        if (task.state.value === 'notDetected') {
                            const item = exports.mcpToolsTreeProvider.getTreeItem(task);
                            assert.ok(item.description, `Not-detected task '${task.entry.label}' should have a description`);
                            assert.ok(item.iconPath, `Not-detected task '${task.entry.label}' should have an icon`);
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

        for (const serverNode of roots) {
            const serverItem = exports.mcpToolsTreeProvider.getTreeItem(serverNode);
            assert.ok(serverItem.tooltip, `Server '${serverNode.name}' should have a tooltip`);

            for (const category of exports.mcpToolsTreeProvider.getChildren(serverNode)) {
                const catItem = exports.mcpToolsTreeProvider.getTreeItem(category);
                assert.ok(catItem.tooltip, `Category '${category.name}' should have a tooltip`);

                for (const tool of exports.mcpToolsTreeProvider.getChildren(category)) {
                    const toolItem = exports.mcpToolsTreeProvider.getTreeItem(tool);
                    assert.ok(toolItem.tooltip, `Tool '${tool.toolName}' should have a tooltip`);

                    for (const task of exports.mcpToolsTreeProvider.getChildren(tool)) {
                        const taskItem = exports.mcpToolsTreeProvider.getTreeItem(task);
                        assert.ok(taskItem.tooltip, `task '${task.entry.label}' should have a tooltip`);
                    }
                }
            }
        }
    });

    test('task tooltips should contain setting ID', async () => {
        const { exports } = await activatedExtension();
        const roots = exports.mcpToolsTreeProvider.getChildren();

        for (const serverNode of roots) {
            for (const category of exports.mcpToolsTreeProvider.getChildren(serverNode)) {
                for (const tool of exports.mcpToolsTreeProvider.getChildren(category)) {
                    for (const task of exports.mcpToolsTreeProvider.getChildren(tool)) {
                        const item = exports.mcpToolsTreeProvider.getTreeItem(task);
                        assert.ok(
                            (item.tooltip as string).includes('Context Key:'),
                            `task tooltip should contain 'Context Key:' prefix`,
                        );
                        assert.ok(
                            (item.tooltip as string).includes(task.entry.contextKey),
                            `task tooltip should contain context key '${task.entry.contextKey}'`,
                        );
                    }
                }
            }
        }
    });
});
