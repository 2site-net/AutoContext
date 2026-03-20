import * as assert from 'node:assert/strict';
import * as vscode from 'vscode';

suite('Extension Smoke Tests', () => {
    const extensionId = 'iam3yal.sharp-pilot';

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
            'sharp-pilot.toggleInstructions',
            'sharp-pilot.toggleTools',
            'sharp-pilot.autoConfigure',
            'sharp-pilot.exportInstructions',
            'sharp-pilot.browseInstructions',
        ];

        for (const cmd of expected) {
            assert.ok(allCommands.includes(cmd), `Command ${cmd} not registered`);
        }
    });
});
