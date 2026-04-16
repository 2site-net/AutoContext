import * as assert from 'node:assert/strict';
import { activatedExtension } from './helpers.js';

suite('Workspace: empty', () => {
    test('should not detect any technology context keys', async () => {
        const { exports } = await activatedExtension();
        const detector = exports.workspaceContextDetector;

        assert.ok(!detector.get('hasTypeScript'), 'Should not detect hasTypeScript');
        assert.ok(!detector.get('hasDotNet'), 'Should not detect hasDotNet');
        assert.ok(!detector.get('hasGit'), 'Should not detect hasGit');
        assert.ok(!detector.get('hasJavaScript'), 'Should not detect hasJavaScript');
        assert.ok(!detector.get('hasNodeJs'), 'Should not detect hasNodeJs');
    });

    test('should surface editorconfig server only', async () => {
        const { exports } = await activatedExtension();
        const defs = await exports.mcpServerProvider.provideMcpServerDefinitions();
        const categories = defs.map((d: { args: string[] }) => d.args[d.args.indexOf('--scope') + 1]);

        assert.ok(categories.includes('editorconfig'), `Expected editorconfig in: ${categories.join(', ')}`);
        assert.strictEqual(categories.length, 1, `Expected exactly 1 server, got: ${categories.join(', ')}`);
    });
});
