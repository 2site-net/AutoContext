// Generates the chatInstructions array in package.json from the instructions
// registry in config.ts. Run: npm run generate:chat-instructions

import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { register } from 'node:module';

// The compiled modules import 'vscode' which isn't available outside the
// extension host.  Register a lightweight ESM loader that resolves 'vscode'
// to an empty stub so the pure-data / pure-function exports we need work.
register('data:text/javascript,' + encodeURIComponent([
    'export function resolve(specifier, context, next) {',
    '  if (specifier === "vscode") return { shortCircuit: true, url: "data:text/javascript,export default {}" };',
    '  return next(specifier, context);',
    '}',
].join('\n')));

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');

const { instructions } = await import(new URL('../out/instructions-catalog.js', import.meta.url));
const { contextKeysForEntry, overrideContextKey } = await import(new URL('../out/toggle-context-keys.js', import.meta.url));

function buildWhenClause(entry) {
    const parts = [`config.${entry.settingId}`];

    const ctxKeys = contextKeysForEntry(entry);
    if (ctxKeys.length === 1) {
        parts.push(`sharppilot.workspace.${ctxKeys[0]}`);
    } else if (ctxKeys.length > 1) {
        parts.push(`(${ctxKeys.map(k => `sharppilot.workspace.${k}`).join(' || ')})`);
    }

    parts.push(`!${overrideContextKey(entry.settingId)}`);

    return parts.join(' && ');
}

const chatInstructions = [
    // Always injected — not toggleable, not filterable, not exportable.
    { path: './instructions/copilot.instructions.md' },
];

for (const entry of instructions) {
    chatInstructions.push({
        path: `./instructions/.generated/${entry.fileName}`,
        when: buildWhenClause(entry),
    });
}

const pkgPath = join(root, 'package.json');
const pkg = JSON.parse(readFileSync(pkgPath, 'utf-8'));

pkg.contributes.chatInstructions = chatInstructions;
writeFileSync(pkgPath, JSON.stringify(pkg, null, 2) + '\n', 'utf-8');

console.log(`Generated ${chatInstructions.length} chatInstructions entries.`);
