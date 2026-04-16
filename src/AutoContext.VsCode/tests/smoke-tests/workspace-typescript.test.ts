import * as assert from 'node:assert/strict';
import { activatedExtension } from './helpers.js';

suite('Workspace: typescript-only', () => {
    test('should detect TypeScript', async () => {
        const { exports } = await activatedExtension();
        const detector = exports.workspaceContextDetector;

        assert.ok(detector.get('hasTypeScript'), 'Expected hasTypeScript');
    });

    test('should NOT detect .NET or Git', async () => {
        const { exports } = await activatedExtension();
        const detector = exports.workspaceContextDetector;

        assert.ok(!detector.get('hasDotNet'), 'Should not detect hasDotNet');
        assert.ok(!detector.get('hasGit'), 'Should not detect hasGit');
    });

    test('should surface editorconfig and typescript servers only', async () => {
        const { exports } = await activatedExtension();
        const defs = await exports.mcpServerProvider.provideMcpServerDefinitions();
        const categories = defs.map((d: { args: string[] }) => d.args[d.args.indexOf('--scope') + 1]);

        assert.ok(categories.includes('editorconfig'), `Expected editorconfig in: ${categories.join(', ')}`);
        assert.ok(categories.includes('typescript'), `Expected typescript in: ${categories.join(', ')}`);
        assert.ok(!categories.includes('dotnet'), `Unexpected dotnet in: ${categories.join(', ')}`);
        assert.ok(!categories.includes('git'), `Unexpected git in: ${categories.join(', ')}`);
        assert.strictEqual(categories.length, 2, `Expected exactly 2 servers, got: ${categories.join(', ')}`);
    });
});
