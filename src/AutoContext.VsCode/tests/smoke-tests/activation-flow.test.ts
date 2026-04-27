import * as assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { activatedExtension } from './helpers.js';

const mcpBinary = (extensionPath: string) =>
    join(extensionPath, 'servers', 'AutoContext.Mcp.Server', `AutoContext.Mcp.Server${process.platform === 'win32' ? '.exe' : ''}`);

suite('Activation Flow Smoke Tests', () => {
    test('workspace detection should complete during activation', async () => {
        const { exports } = await activatedExtension();

        assert.ok(exports.workspaceContextDetector.get('hasTypeScript'), 'hasTypeScript should be set after activation');
    });

    test('context-gated MCP servers should be available after activation without manual detect()', async function () {
        const ext = await activatedExtension();
        if (!existsSync(mcpBinary(ext.extensionPath))) { this.skip(); return; }

        const defs = await ext.exports.mcpServerProvider.provideMcpServerDefinitions();

        assert.ok(defs.length > 0, 'Expected at least one MCP server definition after activation (editorconfig + typescript detected)');
    });
});
