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

    test('should surface MCP server definitions even with no detected technology', async () => {
        const { exports } = await activatedExtension();
        const defs = await exports.mcpServerProvider.provideMcpServerDefinitions();

        assert.ok(defs.length > 0, 'Expected at least one MCP server definition (binary-gated, not context-gated)');
    });
});
