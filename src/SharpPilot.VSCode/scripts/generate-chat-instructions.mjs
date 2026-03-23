// Generates the chatInstructions array in package.json from the instructions
// registry in config.ts. Run: npm run generate:chat-instructions

import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');

const configUrl = new URL('../out/config.js', import.meta.url);
const { instructions, contextKeysForEntry, overrideContextKey, filteredContextKey } =
    await import(configUrl);

function buildWhenClause(entry, filtered) {
    const parts = [`config.${entry.settingId}`];

    const ctxKeys = contextKeysForEntry(entry);
    if (ctxKeys.length === 1) {
        parts.push(`sharp-pilot.workspace.${ctxKeys[0]}`);
    } else if (ctxKeys.length > 1) {
        parts.push(`(${ctxKeys.map(k => `sharp-pilot.workspace.${k}`).join(' || ')})`);
    }

    parts.push(`!${overrideContextKey(entry.settingId)}`);
    parts.push(filtered ? filteredContextKey(entry.settingId) : `!${filteredContextKey(entry.settingId)}`);

    return parts.join(' && ');
}

const chatInstructions = [];

for (const entry of instructions) {
    chatInstructions.push({
        path: `./instructions/${entry.fileName}`,
        when: buildWhenClause(entry, false),
    });

    chatInstructions.push({
        path: `./instructions-filtered/${entry.fileName}`,
        when: buildWhenClause(entry, true),
    });
}

const pkgPath = join(root, 'package.json');
const pkg = JSON.parse(readFileSync(pkgPath, 'utf-8'));

pkg.contributes.chatInstructions = chatInstructions;
writeFileSync(pkgPath, JSON.stringify(pkg, null, 2) + '\n', 'utf-8');

console.log(`Generated ${chatInstructions.length} chatInstructions entries.`);
