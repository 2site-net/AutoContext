import * as assert from 'node:assert/strict';
import { activatedExtension } from './helpers.js';

suite('Workspace: dotnet-only', () => {
    test('should detect .NET', async () => {
        const { exports } = await activatedExtension();
        const detector = exports.workspaceContextDetector;

        assert.ok(detector.get('hasDotNet'), 'Expected hasDotNet');
    });

    test('should NOT detect TypeScript or Git', async () => {
        const { exports } = await activatedExtension();
        const detector = exports.workspaceContextDetector;

        assert.ok(!detector.get('hasTypeScript'), 'Should not detect hasTypeScript');
        assert.ok(!detector.get('hasGit'), 'Should not detect hasGit');
    });

    test('should surface editorconfig and dotnet servers only', async () => {
        const { exports } = await activatedExtension();
        const defs = await exports.mcpServerProvider.provideMcpServerDefinitions();
        const categories = defs.map((d: { args: string[] }) => d.args[d.args.indexOf('--scope') + 1]);

        assert.ok(categories.includes('editorconfig'), `Expected editorconfig in: ${categories.join(', ')}`);
        assert.ok(categories.includes('dotnet'), `Expected dotnet in: ${categories.join(', ')}`);
        assert.ok(!categories.includes('typescript'), `Unexpected typescript in: ${categories.join(', ')}`);
        assert.ok(!categories.includes('git'), `Unexpected git in: ${categories.join(', ')}`);
        assert.strictEqual(categories.length, 2, `Expected exactly 2 servers, got: ${categories.join(', ')}`);
    });
});
