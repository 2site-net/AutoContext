import * as assert from 'node:assert/strict';
import * as vscode from 'vscode';
import { activatedExtension } from './helpers.js';

const taggedFile = 'design-principles.instructions.md';

suite('Content Provider Smoke Tests', () => {
    test('should return content for copilot.instructions.md', async () => {
        const { exports } = await activatedExtension();
        const uri = vscode.Uri.from({ scheme: 'sharppilot-instructions', path: 'copilot.instructions.md' });

        const content = exports.contentProvider.provideTextDocumentContent(uri);

        assert.ok(content.length > 0, 'Content provider returned empty content');
    });

    test('should return content for multiple instruction files', async () => {
        const { exports } = await activatedExtension();
        const files = ['copilot.instructions.md', 'design-principles.instructions.md', 'testing.instructions.md'];

        for (const file of files) {
            const uri = vscode.Uri.from({ scheme: 'sharppilot-instructions', path: file });
            const content = exports.contentProvider.provideTextDocumentContent(uri);
            assert.ok(content.length > 0, `${file} returned empty content`);
        }
    });

    test('should mark disabled instructions in content', async () => {
        const { exports } = await activatedExtension();
        const uri = vscode.Uri.from({ scheme: 'sharppilot-instructions', path: taggedFile });

        const before = exports.contentProvider.provideTextDocumentContent(uri);

        exports.configManager.toggleInstruction(taggedFile, 'INST0001');

        const after = exports.contentProvider.provideTextDocumentContent(uri);

        exports.configManager.resetInstructions(taggedFile);

        assert.ok(!before.includes('[DISABLED]'), 'No instructions should be disabled initially');
        assert.ok(after.includes('[DISABLED]'), 'Disabled instruction should be tagged');
    });
});
