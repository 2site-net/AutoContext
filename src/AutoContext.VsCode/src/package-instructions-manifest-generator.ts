// Builds the chatInstructions manifest from the instructions-files
// manifest and writes it to the contributes.chatInstructions field in
// package.json.
// Self-executable: tsx src/package-instructions-manifest-generator.ts
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
import type { InstructionsFileEntry } from './instructions-file-entry.js';
import type { PackageInstructionsFileEntry } from './types/package-instructions-file-entry.js';

function buildWhenClause(entry: InstructionsFileEntry): string {
    const parts = [entry.runtimeInfo.contextKey];

    const flags = entry.activationFlags;
    if (flags.length === 1) {
        parts.push(`autocontext.workspace.${flags[0]}`);
    } else if (flags.length > 1) {
        parts.push(`(${flags.map(k => `autocontext.workspace.${k}`).join(' || ')})`);
    }

    parts.push(`!${entry.runtimeInfo.overrideKey}`);

    return parts.join(' && ');
}

export function buildChatInstructions(manifest: InstructionsFilesManifest): PackageInstructionsFileEntry[] {
    return [
        { path: './instructions/copilot.instructions.md' },
        ...manifest.instructions.map(entry => ({
            path: `./instructions/.generated/${entry.name}`,
            when: buildWhenClause(entry),
        })),
    ];
}

if (process.argv[1]?.replace(/\\/g, '/').endsWith('/src/package-instructions-manifest-generator.ts')) {
    const root = join(dirname(fileURLToPath(import.meta.url)), '..');
    const pkgPath = join(root, 'package.json');
    const pkg = JSON.parse(readFileSync(pkgPath, 'utf-8'));

    const manifest = new InstructionsFilesManifestLoader(root).load();
    pkg.contributes.chatInstructions = buildChatInstructions(manifest);
    writeFileSync(pkgPath, JSON.stringify(pkg, null, 2) + '\n', 'utf-8');

    console.log(`Generated ${pkg.contributes.chatInstructions.length} chatInstructions entries.`);
}
