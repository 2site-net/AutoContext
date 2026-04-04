import * as assert from 'node:assert/strict';
import * as vscode from 'vscode';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { activatedExtension } from './helpers.js';

suite('Extension Smoke Tests', () => {
    test('extension should be present', () => {
        const ext = vscode.extensions.getExtension('2site-net.sharppilot');

        assert.ok(ext, 'Extension 2site-net.sharppilot not found');
    });

    test('extension should activate', async () => {
        const ext = await activatedExtension();

        assert.ok(ext.isActive, 'Extension did not activate');
    });

    test('registered commands should include all SharpPilot commands', async () => {
        await activatedExtension();

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
        const missing = expected.filter(cmd => !allCommands.includes(cmd));

        assert.ok(missing.length === 0, `Missing commands: ${missing.join(', ')}`);
    });

    test('generated instructions should be normalized (tags stripped)', async () => {
        const ext = await activatedExtension();
        const generatedDir = join(ext.extensionPath, 'instructions', '.generated');
        assert.ok(existsSync(generatedDir), `.generated directory not found at ${generatedDir}`);
        const files = readdirSync(generatedDir).filter((f: string) => f.endsWith('.md'));
        assert.ok(files.length > 0, '.generated directory contains no instruction files');
        const violations = files.filter(file => {
            const content = readFileSync(join(generatedDir, file), 'utf8');
            return content.length === 0 || /\[INST\d{4}]/.test(content);
        });

        assert.ok(violations.length === 0, `Files with empty content or un-stripped tags: ${violations.join(', ')}`);
    });

    test('sharppilot-instructions content provider should be registered', async () => {
        await activatedExtension();
        const uri = vscode.Uri.from({ scheme: 'sharppilot-instructions', path: 'copilot.instructions.md' });

        const doc = await vscode.workspace.openTextDocument(uri);

        assert.ok(doc.getText().length > 0, 'Content provider returned empty document');
    });
});
