// Builds the chatInstructions manifest from the instructions-files
// manifest and writes it to the contributes.chatInstructions field in
// package.json.
// Self-executable: tsx src/chat-instructions-files-manifest.ts
//
// Example output:
//   { "path": "./instructions/copilot.instructions.md" },
//   { "path": "./instructions/.generated/code-review.instructions.md",
//     "when": "autocontext.instructions.code-review && !autocontext.override.code-review" },
//   { "path": "./instructions/.generated/testing.instructions.md",
//     "when": "autocontext.instructions.testing && (autocontext.workspace.hasDotNetTesting || autocontext.workspace.hasWebTesting) && !autocontext.override.testing" }

import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { InstructionsFilesManifestLoader } from './instructions-files-manifest-loader.js';
import { InstructionsFilesManifest } from './instructions-files-manifest.js';
import { ContextKeys } from './context-keys.js';
import type { CatalogEntry } from './types/catalog-entry.js';
import type { ChatInstructions } from './types/chat-instructions.js';

function buildWhenClause(entry: CatalogEntry): string {
    const parts = [entry.contextKey];

    const flags = ContextKeys.forEntry(entry);
    if (flags.length === 1) {
        parts.push(`autocontext.workspace.${flags[0]}`);
    } else if (flags.length > 1) {
        parts.push(`(${flags.map(k => `autocontext.workspace.${k}`).join(' || ')})`);
    }

    parts.push(`!${ContextKeys.overrideKey(entry.contextKey)}`);

    return parts.join(' && ');
}

export function buildChatInstructions(manifest: InstructionsFilesManifest): ChatInstructions[] {
    return [
        { path: './instructions/copilot.instructions.md' },
        ...manifest.instructions.map(entry => ({
            path: `./instructions/.generated/${entry.name}`,
            when: buildWhenClause(entry),
        })),
    ];
}

if (process.argv[1]?.replace(/\\/g, '/').endsWith('/src/chat-instructions-files-manifest.ts')) {
    const root = join(dirname(fileURLToPath(import.meta.url)), '..');
    const pkgPath = join(root, 'package.json');
    const pkg = JSON.parse(readFileSync(pkgPath, 'utf-8'));

    const manifest = new InstructionsFilesManifestLoader(root).load();
    pkg.contributes.chatInstructions = buildChatInstructions(manifest);
    writeFileSync(pkgPath, JSON.stringify(pkg, null, 2) + '\n', 'utf-8');

    console.log(`Generated ${pkg.contributes.chatInstructions.length} chatInstructions entries.`);
}
