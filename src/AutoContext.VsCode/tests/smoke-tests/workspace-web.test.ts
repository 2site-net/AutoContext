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

    test('should surface MCP server definitions for web-only workspace', async () => {
        const { exports } = await activatedExtension();
        const defs = await exports.mcpServerProvider.provideMcpServerDefinitions();

        assert.ok(defs.length > 0, 'Expected at least one MCP server definition for web-only workspace');
    });
});
