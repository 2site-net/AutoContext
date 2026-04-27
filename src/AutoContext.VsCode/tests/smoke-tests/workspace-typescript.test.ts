import * as assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { activatedExtension } from './helpers.js';

const mcpBinary = (extensionPath: string) =>
    join(extensionPath, 'servers', 'AutoContext.Mcp.Server', `AutoContext.Mcp.Server${process.platform === 'win32' ? '.exe' : ''}`);

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

    test('should surface MCP server definitions for TypeScript workspace', async function () {
        const ext = await activatedExtension();
        if (!existsSync(mcpBinary(ext.extensionPath))) { this.skip(); return; }

        const defs = await ext.exports.mcpServerProvider.provideMcpServerDefinitions();

        assert.ok(defs.length > 0, 'Expected at least one MCP server definition for TypeScript workspace');
    });
});
