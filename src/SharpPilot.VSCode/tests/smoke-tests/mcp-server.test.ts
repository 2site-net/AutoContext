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
        for (const def of defs) {
            const idx = def.args.indexOf('--scope');
            assert.ok(idx >= 0, `Definition ${def.label} missing --scope`);
            assert.ok(def.args[idx + 1], `Definition ${def.label} has empty --scope value`);
        }
    });

    test('every definition should carry the extension version', async () => {
        const { exports } = await activatedExtension();

        const defs = await exports.mcpServerProvider.provideMcpServerDefinitions();
        for (const def of defs) {
            assert.ok(def.version, `Definition ${def.label} missing version`);
        }
    });

    test('should include TypeScript category for this workspace', async () => {
        const { exports } = await activatedExtension();
        await exports.workspaceContextDetector.detect();

        const defs = await exports.mcpServerProvider.provideMcpServerDefinitions();

        const categories = defs.map((d: { args: string[] }) => d.args[d.args.indexOf('--scope') + 1]);
        assert.ok(categories.includes('typescript'), `Expected typescript in categories: ${categories.join(', ')}`);
    });
});
