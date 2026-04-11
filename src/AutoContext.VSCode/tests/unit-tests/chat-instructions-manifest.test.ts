import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildChatInstructions } from '../../src/chat-instructions-manifest';
import { InstructionsCatalog } from '../../src/instructions-catalog';
import { instructionsFiles } from '../../src/ui-constants';

describe('chatInstructions in package.json', () => {
    const pkgPath = join(dirname(fileURLToPath(import.meta.url)), '../../package.json');
    const pkg = JSON.parse(readFileSync(pkgPath, 'utf-8'));
    const catalog = new InstructionsCatalog(instructionsFiles);

    it('should match the instructions catalog', () => {
        expect.soft(pkg.contributes.chatInstructions).toEqual(buildChatInstructions(catalog));
    });
});
