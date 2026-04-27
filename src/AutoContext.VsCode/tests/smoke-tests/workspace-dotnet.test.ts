import * as assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { activatedExtension } from './helpers.js';

const mcpBinary = (extensionPath: string) =>
    join(extensionPath, 'servers', 'AutoContext.Mcp.Server', `AutoContext.Mcp.Server${process.platform === 'win32' ? '.exe' : ''}`);

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

    test('should surface MCP server definitions for dotnet workspace', async function () {
        const ext = await activatedExtension();
        if (!existsSync(mcpBinary(ext.extensionPath))) { this.skip(); return; }

        const defs = await ext.exports.mcpServerProvider.provideMcpServerDefinitions();

        assert.ok(defs.length > 0, 'Expected at least one MCP server definition for dotnet workspace');
    });
});
