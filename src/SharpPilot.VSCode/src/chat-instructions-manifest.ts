// Builds the chatInstructions manifest from the instructions catalog.
// Self-executable: tsx src/chat-instructions-manifest.ts

import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { instructionsCatalog } from './instructions-catalog.js';
import { ContextKeys } from './context-keys.js';
import type { CatalogEntry } from './catalog-entry.js';

export interface ChatInstruction {
    path: string;
    when?: string;
}

function buildWhenClause(entry: CatalogEntry): string {
    const parts = [`config.${entry.settingId}`];

    const ctxKeys = ContextKeys.forEntry(entry);
    if (ctxKeys.length === 1) {
        parts.push(`sharppilot.workspace.${ctxKeys[0]}`);
    } else if (ctxKeys.length > 1) {
        parts.push(`(${ctxKeys.map(k => `sharppilot.workspace.${k}`).join(' || ')})`);
    }

    parts.push(`!${ContextKeys.overrideKey(entry.settingId)}`);

    return parts.join(' && ');
}

export function buildChatInstructions(): ChatInstruction[] {
    return [
        { path: './instructions/copilot.instructions.md' },
        ...instructionsCatalog.all.map(entry => ({
            path: `./instructions/.generated/${entry.fileName}`,
            when: buildWhenClause(entry),
        })),
    ];
}

if (process.argv[1]?.replace(/\\/g, '/').endsWith('/src/chat-instructions-manifest.ts')) {
    const root = join(dirname(fileURLToPath(import.meta.url)), '..');
    const pkgPath = join(root, 'package.json');
    const pkg = JSON.parse(readFileSync(pkgPath, 'utf-8'));

    pkg.contributes.chatInstructions = buildChatInstructions();
    writeFileSync(pkgPath, JSON.stringify(pkg, null, 2) + '\n', 'utf-8');

    console.log(`Generated ${pkg.contributes.chatInstructions.length} chatInstructions entries.`);
}
