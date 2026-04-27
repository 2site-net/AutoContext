import * as assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { activatedExtension } from './helpers.js';

const mcpBinary = (extensionPath: string) =>
    join(extensionPath, 'servers', 'AutoContext.Mcp.Server', `AutoContext.Mcp.Server${process.platform === 'win32' ? '.exe' : ''}`);

suite('MCP Server Provider Smoke Tests', () => {
    test('should return at least one server definition', async function () {
        const ext = await activatedExtension();
        if (!existsSync(mcpBinary(ext.extensionPath))) { this.skip(); return; }

        const defs = await ext.exports.mcpServerProvider.provideMcpServerDefinitions();

        assert.ok(defs.length > 0, 'No MCP server definitions returned');
    });

    test('every definition should carry the endpoint-suffix and health-monitor args', async () => {
        const { exports } = await activatedExtension();

        const defs = await exports.mcpServerProvider.provideMcpServerDefinitions();

        const missing = defs.filter((d: { label: string; args: string[] }) =>
            !d.args.includes('--endpoint-suffix') || !d.args.includes('--health-monitor'));

        assert.strictEqual(
            missing.length,
            0,
            `Definitions missing --endpoint-suffix or --health-monitor: ${missing.map((d: { label: string }) => d.label).join(', ')}`,
        );
    });

    test('every definition should carry the extension version', async () => {
        const { exports } = await activatedExtension();

        const defs = await exports.mcpServerProvider.provideMcpServerDefinitions();

        const missing = defs.filter((d: { label: string; version?: string }) => !d.version);

        assert.strictEqual(missing.length, 0, `Definitions missing version: ${missing.map((d: { label: string }) => d.label).join(', ')}`);
    });

    test('should return a server definition for this TypeScript workspace', async function () {
        const ext = await activatedExtension();
        if (!existsSync(mcpBinary(ext.extensionPath))) { this.skip(); return; }

        await ext.exports.workspaceContextDetector.detect();
        const defs = await ext.exports.mcpServerProvider.provideMcpServerDefinitions();

        assert.ok(defs.length > 0, 'Expected at least one server definition after TypeScript workspace detection');
    });
});
