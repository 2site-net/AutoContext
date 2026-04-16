import * as assert from 'node:assert/strict';
import { activatedExtension } from './helpers.js';

suite('Workspace: web-only (JS + package.json, no TypeScript)', () => {
    test('should detect JavaScript and Node.js', async () => {
        const { exports } = await activatedExtension();
        const detector = exports.workspaceContextDetector;

        assert.ok(detector.get('hasJavaScript'), 'Expected hasJavaScript');
        assert.ok(detector.get('hasNodeJs'), 'Expected hasNodeJs');
    });

    test('should NOT detect TypeScript, .NET, or Git', async () => {
        const { exports } = await activatedExtension();
        const detector = exports.workspaceContextDetector;

        assert.ok(!detector.get('hasTypeScript'), 'Should not detect hasTypeScript');
        assert.ok(!detector.get('hasDotNet'), 'Should not detect hasDotNet');
        assert.ok(!detector.get('hasGit'), 'Should not detect hasGit');
    });

    test('should surface editorconfig server only', async () => {
        const { exports } = await activatedExtension();
        const defs = await exports.mcpServerProvider.provideMcpServerDefinitions();
        const categories = defs.map((d: { args: string[] }) => d.args[d.args.indexOf('--scope') + 1]);

        assert.ok(categories.includes('editorconfig'), `Expected editorconfig in: ${categories.join(', ')}`);
        assert.ok(!categories.includes('typescript'), `Unexpected typescript in: ${categories.join(', ')}`);
        assert.ok(!categories.includes('dotnet'), `Unexpected dotnet in: ${categories.join(', ')}`);
        assert.ok(!categories.includes('git'), `Unexpected git in: ${categories.join(', ')}`);
        assert.strictEqual(categories.length, 1, `Expected exactly 1 server, got: ${categories.join(', ')}`);
    });
});
