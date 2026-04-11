import * as assert from 'node:assert/strict';
import { activatedExtension } from './helpers.js';

suite('MCP Server Provider Smoke Tests', () => {
    test('should return at least one server definition', async () => {
        const { exports } = await activatedExtension();
        const defs = await exports.mcpServerProvider.provideMcpServerDefinitions();

        assert.ok(defs.length > 0, 'No MCP server definitions returned');
    });

    test('every definition should carry --scope with a category', async () => {
        const { exports } = await activatedExtension();

        const defs = await exports.mcpServerProvider.provideMcpServerDefinitions();

        const missing = defs.filter((d: { label: string; args: string[] }) => {
            const idx = d.args.indexOf('--scope');
            return idx < 0 || !d.args[idx + 1];
        });

        assert.strictEqual(missing.length, 0, `Definitions missing --scope: ${missing.map((d: { label: string }) => d.label).join(', ')}`);
    });

    test('every definition should carry the extension version', async () => {
        const { exports } = await activatedExtension();

        const defs = await exports.mcpServerProvider.provideMcpServerDefinitions();

        const missing = defs.filter((d: { label: string; version?: string }) => !d.version);

        assert.strictEqual(missing.length, 0, `Definitions missing version: ${missing.map((d: { label: string }) => d.label).join(', ')}`);
    });

    test('should include TypeScript category for this workspace', async () => {
        const { exports } = await activatedExtension();
        await exports.workspaceContextDetector.detect();

        const defs = await exports.mcpServerProvider.provideMcpServerDefinitions();

        const categories = defs.map((d: { args: string[] }) => d.args[d.args.indexOf('--scope') + 1]);
        assert.ok(categories.includes('typescript'), `Expected typescript in categories: ${categories.join(', ')}`);
    });
});
