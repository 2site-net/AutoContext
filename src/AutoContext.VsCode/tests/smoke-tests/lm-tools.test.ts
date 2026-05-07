import * as assert from 'node:assert/strict';
import * as vscode from 'vscode';
import { activatedExtension } from './helpers.js';

const expectedToolNames = [
    'list_autocontext_instructions_files',
    'search_autocontext_instructions_files_by_metadata',
    'search_autocontext_instructions_files_by_content',
    'get_autocontext_instructions_file',
];

suite('LM Tools Smoke Tests', () => {
    test('vscode.lm.tools should expose all four AutoContext instructions tools', async () => {
        await activatedExtension();

        const registered = vscode.lm.tools.map(t => t.name);
        const missing = expectedToolNames.filter(name => !registered.includes(name));

        assert.strictEqual(missing.length, 0, `Missing LM tools: ${missing.join(', ')}`);
    });

    test('list_autocontext_instructions_files should return a non-empty catalogue with no args', async () => {
        const { exports } = await activatedExtension();

        const result = await exports.lmToolsListHandler.handle({});

        assert.strictEqual(result.kind, 'ok', `Expected ok, got ${JSON.stringify(result)}`);
        assert.ok(result.results.length > 0, 'Expected at least one catalogue entry from list_*');
        const sample = result.results[0];
        assert.ok(typeof sample.name === 'string' && sample.name.endsWith('.instructions.md'),
            `Expected entry.name to be an instructions filename, got: ${JSON.stringify(sample)}`);
    });

    test('get_autocontext_instructions_file should return projected body with no [INST####] tags', async () => {
        const { exports } = await activatedExtension();

        const result = await exports.lmToolsGetHandler.handle({ name: 'lang-typescript.instructions.md' });

        assert.strictEqual(result.kind, 'ok', `Expected ok, got ${JSON.stringify(result)}`);
        assert.ok(result.content.length > 0, 'Projected content should not be empty');
        assert.ok(!/\[INST\d{4}]/.test(result.content), 'Projected content should not contain [INST####] tags');
    });

    test('search_autocontext_instructions_files_by_content should return at least one hit for a common query', async () => {
        const { exports } = await activatedExtension();

        const result = await exports.lmToolsSearchByContentHandler.handle({ query: 'testing' });

        assert.strictEqual(result.kind, 'ok', `Expected ok, got ${JSON.stringify(result)}`);
        assert.ok(result.results.length > 0, 'Expected at least one content-search hit for "testing"');
    });
});
