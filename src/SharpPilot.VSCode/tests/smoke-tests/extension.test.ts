import * as assert from 'node:assert/strict';
import * as vscode from 'vscode';

suite('Extension Smoke Tests', () => {
    const extensionId = '2site-net.sharppilot';

    test('extension should be present', () => {
        const ext = vscode.extensions.getExtension(extensionId);
        assert.ok(ext, `Extension ${extensionId} not found`);
    });

    test('extension should activate', async () => {
        const ext = vscode.extensions.getExtension(extensionId);
        assert.ok(ext, `Extension ${extensionId} not found`);
        await ext.activate();
        assert.ok(ext.isActive, 'Extension did not activate');
    });

    test('registered commands should include all SharpPilot commands', async () => {
        const allCommands = await vscode.commands.getCommands(true);
        const expected = [
            'sharppilot.toggleInstructions',
            'sharppilot.toggleTools',
            'sharppilot.autoConfigure',
            'sharppilot.exportInstructions',
            'sharppilot.browseInstructions',
            'sharppilot.toggleInstruction',
            'sharppilot.resetInstructions',
        ];

        for (const cmd of expected) {
            assert.ok(allCommands.includes(cmd), `Command ${cmd} not registered`);
        }
    });
});
