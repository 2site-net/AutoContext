import * as assert from 'node:assert/strict';
import * as vscode from 'vscode';
import { activatedExtension } from './helpers.js';

const taggedFile = 'design-principles.instructions.md';

suite('CodeLens Provider Smoke Tests', () => {
    test('should return lenses for an instruction file', async () => {
        const { exports } = await activatedExtension();
        const uri = vscode.Uri.from({ scheme: 'sharppilot-instructions', path: taggedFile });
        const doc = await vscode.workspace.openTextDocument(uri);
        const lenses = exports.codeLensProvider.provideCodeLenses(doc);

        assert.ok(lenses.length > 0, 'No CodeLens items returned');
    });

    test('every lens should carry a toggle or reset command', async () => {
        const { exports } = await activatedExtension();
        const uri = vscode.Uri.from({ scheme: 'sharppilot-instructions', path: taggedFile });
        const doc = await vscode.workspace.openTextDocument(uri);

        const lenses = exports.codeLensProvider.provideCodeLenses(doc);

        const validCommands = ['sharppilot.toggleInstruction', 'sharppilot.resetInstructions'];
        const invalid = lenses.filter((l: vscode.CodeLens) => !validCommands.includes((l.command as { command: string }).command));

        assert.strictEqual(invalid.length, 0, `Unexpected commands: ${invalid.map((l: vscode.CodeLens) => (l.command as { command: string }).command).join(', ')}`);
    });

    test('lenses should include Disable Instruction titles when no instructions are disabled', async () => {
        const { exports } = await activatedExtension();
        const uri = vscode.Uri.from({ scheme: 'sharppilot-instructions', path: taggedFile });
        const doc = await vscode.workspace.openTextDocument(uri);
        const lenses = exports.codeLensProvider.provideCodeLenses(doc);
        const titles = lenses.map((l: vscode.CodeLens) => (l.command as { title: string }).title);

        assert.ok(titles.some((t: string) => t.includes('Disable Instruction')), 'Expected at least one Disable Instruction lens');
    });
});
