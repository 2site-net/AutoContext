import * as assert from 'node:assert/strict';
import * as vscode from 'vscode';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

suite('Extension Smoke Tests', () => {
    const extensionId = '2site-net.sharppilot';

    async function activatedExtension(): Promise<vscode.Extension<unknown>> {
        const ext = vscode.extensions.getExtension(extensionId);
        assert.ok(ext, `Extension ${extensionId} not found`);
        if (!ext.isActive) {
            await ext.activate();
        }
        return ext as vscode.Extension<unknown>;
    }

    test('extension should be present', () => {
        const ext = vscode.extensions.getExtension(extensionId);
        assert.ok(ext, `Extension ${extensionId} not found`);
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

        for (const cmd of expected) {
            assert.ok(allCommands.includes(cmd), `Command ${cmd} not registered`);
        }
    });

    test('generated instructions should be normalized (tags stripped)', async () => {
        const ext = await activatedExtension();
        const generatedDir = join(ext.extensionPath, 'instructions', '.generated');
        assert.ok(existsSync(generatedDir), `.generated directory not found at ${generatedDir}`);

        const files = readdirSync(generatedDir).filter((f: string) => f.endsWith('.md'));
        assert.ok(files.length > 0, '.generated directory contains no instruction files');

        // The source files contain [INSTxxxx] tags; normalized output must not.
        for (const file of files) {
            const content = readFileSync(join(generatedDir, file), 'utf8');
            assert.ok(content.length > 0, `${file} is empty`);
            assert.ok(!/\[INST\d{4}]/.test(content), `${file} still contains [INSTxxxx] tags — normalization did not run`);
        }
    });

    test('sharppilot-instructions content provider should be registered', async () => {
        await activatedExtension();
        const uri = vscode.Uri.from({ scheme: 'sharppilot-instructions', path: 'copilot.instructions.md' });
        const doc = await vscode.workspace.openTextDocument(uri);
        assert.ok(doc.getText().length > 0, 'Content provider returned empty document');
    });
});
