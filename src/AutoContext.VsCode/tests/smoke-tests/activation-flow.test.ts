import * as assert from 'node:assert/strict';
import { activatedExtension } from './helpers.js';

suite('Activation Flow Smoke Tests', () => {
    test('workspace detection should complete during activation', async () => {
        const { exports } = await activatedExtension();

        assert.ok(exports.workspaceContextDetector.get('hasTypeScript'), 'hasTypeScript should be set after activation');
    });

    test('context-gated MCP servers should be available after activation without manual detect()', async () => {
        const { exports } = await activatedExtension();

        const defs = await exports.mcpServerProvider.provideMcpServerDefinitions();
        const categories = defs.map((d: { args: string[] }) => d.args[d.args.indexOf('--scope') + 1]);

        assert.ok(categories.includes('editorconfig'), `Expected editorconfig (ungated) in: ${categories.join(', ')}`);
        assert.ok(categories.includes('typescript'), `Expected typescript (context-gated) in: ${categories.join(', ')}`);
    });
});
