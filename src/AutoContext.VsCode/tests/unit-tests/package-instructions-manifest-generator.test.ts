import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildChatInstructions } from '#src/package-instructions-manifest-generator';
import { InstructionsFilesManifestLoader } from '#src/instructions-files-manifest-loader';

describe('chatInstructions in package.json', () => {
    const root = join(dirname(fileURLToPath(import.meta.url)), '../..');
    const pkg = JSON.parse(readFileSync(join(root, 'package.json'), 'utf-8'));
    const manifest = new InstructionsFilesManifestLoader(root).load();

    it('should match the instructions manifest', () => {
        expect.soft(pkg.contributes.chatInstructions).toEqual(buildChatInstructions(manifest));
    });
});
