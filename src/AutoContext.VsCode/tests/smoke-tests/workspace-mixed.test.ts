import * as assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { activatedExtension } from './helpers.js';

const mcpBinary = (extensionPath: string) =>
    join(extensionPath, 'servers', 'AutoContext.Mcp.Server', `AutoContext.Mcp.Server${process.platform === 'win32' ? '.exe' : ''}`);

suite('Workspace: mixed (TypeScript + .NET + Git)', () => {
    test('should detect TypeScript, .NET, and Git context keys', async () => {
        const { exports } = await activatedExtension();
        const detector = exports.workspaceContextDetector;

        assert.ok(detector.get('hasTypeScript'), 'Expected hasTypeScript');
        assert.ok(detector.get('hasDotNet'), 'Expected hasDotNet');
        assert.ok(detector.get('hasGit'), 'Expected hasGit');
    });

    test('should surface MCP server definitions for all detected contexts', async function () {
        const ext = await activatedExtension();
        if (!existsSync(mcpBinary(ext.extensionPath))) { this.skip(); return; }

        const defs = await ext.exports.mcpServerProvider.provideMcpServerDefinitions();

        assert.ok(defs.length > 0, 'Expected at least one MCP server definition for mixed workspace');
    });
});
