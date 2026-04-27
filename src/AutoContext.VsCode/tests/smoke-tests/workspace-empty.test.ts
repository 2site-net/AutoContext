import * as assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { activatedExtension } from './helpers.js';

const mcpBinary = (extensionPath: string) =>
    join(extensionPath, 'servers', 'AutoContext.Mcp.Server', `AutoContext.Mcp.Server${process.platform === 'win32' ? '.exe' : ''}`);

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

    test('should not surface MCP server definitions without binary', async function () {
        const ext = await activatedExtension();
        if (existsSync(mcpBinary(ext.extensionPath))) { this.skip(); return; }

        const defs = await ext.exports.mcpServerProvider.provideMcpServerDefinitions();

        assert.strictEqual(defs.length, 0, 'Expected no MCP server definitions without binary');
    });
});
