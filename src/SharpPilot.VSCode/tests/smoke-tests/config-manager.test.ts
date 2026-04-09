import * as assert from 'node:assert/strict';
import * as vscode from 'vscode';
import { activatedExtension } from './helpers.js';

const taggedFile = 'design-principles.instructions.md';

suite('Config Manager Smoke Tests', () => {
    teardown(async () => {
        const { exports } = await activatedExtension();
        await exports.configManager.resetInstructions(taggedFile);
    });

    test('should have no disabled instructions by default', async () => {
        const { exports } = await activatedExtension();

        assert.strictEqual(await exports.configManager.hasAnyDisabledInstructions(), false);
    });

    test('toggle should disable an instruction', async () => {
        const { exports } = await activatedExtension();

        await vscode.commands.executeCommand('sharppilot.toggle-instruction', taggedFile, 'INST0001');

        const disabled = await exports.configManager.getDisabledInstructions(taggedFile);

        assert.ok(disabled.has('INST0001'), 'INST0001 should be disabled after toggle');
    });

    test('reset should re-enable all instructions for a file', async () => {
        const { exports } = await activatedExtension();

        await vscode.commands.executeCommand('sharppilot.toggle-instruction', taggedFile, 'INST0001');
        await vscode.commands.executeCommand('sharppilot.reset-instructions', taggedFile);

        const disabled = await exports.configManager.getDisabledInstructions(taggedFile);

        assert.strictEqual(disabled.size, 0, 'No instructions should be disabled after reset');
    });

    test('removeOrphanedIds should not throw', async () => {
        const { exports } = await activatedExtension();

        await exports.configManager.removeOrphanedIds();
    });
});
