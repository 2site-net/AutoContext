import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildChatInstructions } from '#src/package-instructions-manifest-generator';
import { InstructionsFilesManifestLoader } from '#src/instructions-files-manifest-loader';
import { createFakeConfigManager } from '#support/fake-config-manager';
import { createFakeDetector } from '#support/fake-detector';
import { createFakeOverrideWatcher } from '#support/fake-override-watcher';

describe('chatInstructions in package.json', () => {
    const root = join(dirname(fileURLToPath(import.meta.url)), '../..');
    const pkg = JSON.parse(readFileSync(join(root, 'package.json'), 'utf-8'));
    const manifest = new InstructionsFilesManifestLoader(root, {
        detector: createFakeDetector(),
        overrideWatcher: createFakeOverrideWatcher(),
        configManager: createFakeConfigManager(),
    }).load();

    it('should match the instructions manifest', () => {
        expect.soft(pkg.contributes.chatInstructions).toEqual(buildChatInstructions(manifest));
    });
});
