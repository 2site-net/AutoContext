import * as assert from 'node:assert/strict';
import { activatedExtension } from './helpers.js';

suite('Workspace: mixed (TypeScript + .NET + Git)', () => {
    test('should detect TypeScript, .NET, and Git context keys', async () => {
        const { exports } = await activatedExtension();
        const detector = exports.workspaceContextDetector;

        assert.ok(detector.get('hasTypeScript'), 'Expected hasTypeScript');
        assert.ok(detector.get('hasDotNet'), 'Expected hasDotNet');
        assert.ok(detector.get('hasGit'), 'Expected hasGit');
    });

    test('should surface all four MCP servers', async () => {
        const { exports } = await activatedExtension();
        const defs = await exports.mcpServerProvider.provideMcpServerDefinitions();
        const categories = defs.map((d: { args: string[] }) => d.args[d.args.indexOf('--scope') + 1]);

        assert.ok(categories.includes('editorconfig'), `Expected editorconfig in: ${categories.join(', ')}`);
        assert.ok(categories.includes('typescript'), `Expected typescript in: ${categories.join(', ')}`);
        assert.ok(categories.includes('dotnet'), `Expected dotnet in: ${categories.join(', ')}`);
        assert.ok(categories.includes('git'), `Expected git in: ${categories.join(', ')}`);
        assert.strictEqual(categories.length, 4, `Expected exactly 4 servers, got: ${categories.join(', ')}`);
    });
});
