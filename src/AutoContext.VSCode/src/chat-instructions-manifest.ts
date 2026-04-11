// Builds the chatInstructions manifest from the instructions catalog and writes
// it to the contributes.chatInstructions field in package.json.
// Self-executable: tsx src/chat-instructions-manifest.ts
//
// Example output:
//   { "path": "./instructions/copilot.instructions.md" },
//   { "path": "./instructions/.generated/code-review.instructions.md",
//     "when": "config.autocontext.instructions.codeReview && !autocontext.override.codeReview" },
//   { "path": "./instructions/.generated/testing.instructions.md",
//     "when": "config.autocontext.instructions.testing && (autocontext.workspace.hasDotNetTesting || autocontext.workspace.hasWebTesting) && !autocontext.override.testing" }

import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { InstructionsCatalog } from './instructions-catalog.js';
import { instructionsFiles } from './ui-constants.js';
import { ContextKeys } from './context-keys.js';
import type { CatalogEntry } from './types/catalog-entry.js';
import type { ChatInstructions } from './types/chat-instructions.js';

function buildWhenClause(entry: CatalogEntry): string {
    const parts = [`config.${entry.settingId}`];

    const ctxKeys = ContextKeys.forEntry(entry);
    if (ctxKeys.length === 1) {
        parts.push(`autocontext.workspace.${ctxKeys[0]}`);
    } else if (ctxKeys.length > 1) {
        parts.push(`(${ctxKeys.map(k => `autocontext.workspace.${k}`).join(' || ')})`);
    }

    parts.push(`!${ContextKeys.overrideKey(entry.settingId)}`);

    return parts.join(' && ');
}

export function buildChatInstructions(catalog: InstructionsCatalog): ChatInstructions[] {
    return [
        { path: './instructions/copilot.instructions.md' },
        ...catalog.all.map(entry => ({
            path: `./instructions/.generated/${entry.fileName}`,
            when: buildWhenClause(entry),
        })),
    ];
}

if (process.argv[1]?.replace(/\\/g, '/').endsWith('/src/chat-instructions-manifest.ts')) {
    const root = join(dirname(fileURLToPath(import.meta.url)), '..');
    const pkgPath = join(root, 'package.json');
    const pkg = JSON.parse(readFileSync(pkgPath, 'utf-8'));

    const catalog = new InstructionsCatalog(instructionsFiles);
    pkg.contributes.chatInstructions = buildChatInstructions(catalog);
    writeFileSync(pkgPath, JSON.stringify(pkg, null, 2) + '\n', 'utf-8');

    console.log(`Generated ${pkg.contributes.chatInstructions.length} chatInstructions entries.`);
}
