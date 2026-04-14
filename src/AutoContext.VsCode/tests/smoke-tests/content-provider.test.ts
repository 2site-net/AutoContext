import * as assert from 'node:assert/strict';
import * as vscode from 'vscode';
import { activatedExtension } from './helpers.js';

const taggedFile = 'design-principles.instructions.md';

suite('Content Provider Smoke Tests', () => {
    test('should return content for copilot.instructions.md', async () => {
        const { exports } = await activatedExtension();
        const uri = vscode.Uri.from({ scheme: 'autocontext-instructions', path: 'copilot.instructions.md' });
        const content = await exports.contentProvider.provideTextDocumentContent(uri);

        assert.ok(content.length > 0, 'Content provider returned empty content');
    });

    test('should return content for multiple instruction files', async () => {
        const { exports } = await activatedExtension();
        const files = ['copilot.instructions.md', 'design-principles.instructions.md', 'testing.instructions.md'];

        const empty: string[] = [];
        for (const file of files) {
            const uri = vscode.Uri.from({ scheme: 'autocontext-instructions', path: file });
            const content = await exports.contentProvider.provideTextDocumentContent(uri);
            if (content.length === 0) empty.push(file);
        }

        assert.strictEqual(empty.length, 0, `Empty content for: ${empty.join(', ')}`);
    });

    test('should mark disabled instructions in content', async () => {
        const { exports } = await activatedExtension();
        const uri = vscode.Uri.from({ scheme: 'autocontext-instructions', path: taggedFile });

        const before = await exports.contentProvider.provideTextDocumentContent(uri);

        await exports.configManager.toggleInstruction(taggedFile, 'INST0001');

        const after = await exports.contentProvider.provideTextDocumentContent(uri);

        await exports.configManager.resetInstructions(taggedFile);

        assert.ok(!before.includes('[DISABLED]'), 'No instructions should be disabled initially');
        assert.ok(after.includes('[DISABLED]'), 'Disabled instruction should be tagged');
    });
});
