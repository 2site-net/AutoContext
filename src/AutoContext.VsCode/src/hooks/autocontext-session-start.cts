#!/usr/bin/env node
// AutoContext SessionStart hook (shipped via the bundled agent-plugin).
//
// Reads the two always-attached instruction files shipped with the
// AutoContext extension and emits their bodies as additionalContext
// so they are injected into every new chat session, in addition to
// the `chatInstructions` manifest entries.
//
// Layout (inside the installed extension):
//   <extension>/instructions/copilot.instructions.md
//   <extension>/instructions/autocontext.instructions.md
//   <extension>/plugin/.claude-plugin/plugin.json
//   <extension>/plugin/hooks/hooks.json
//   <extension>/plugin/scripts/autocontext-session-start.cjs   ← compiled output
//
// Authored as `.cts` so TypeScript emits CommonJS regardless of the
// extension's `"type": "module"` package.json. `tsc` writes the
// resulting `.cjs` into `dist/hooks/`; the build then copies it into
// `plugin/scripts/` so it sits alongside the plugin manifest.

import * as fs from 'node:fs';
import * as path from 'node:path';

// __dirname = <extension>/plugin/scripts
// EXTENSION_ROOT = <extension>
const EXTENSION_ROOT = path.resolve(__dirname, '..', '..');
const INSTRUCTIONS_DIR = path.join(EXTENSION_ROOT, 'instructions');

const FILES: readonly string[] = [
    'copilot.instructions.md',
    'autocontext.instructions.md',
];

interface SessionStartOutput {
    readonly hookSpecificOutput: {
        readonly hookEventName: 'SessionStart';
        readonly additionalContext: string;
    };
}

/**
 * Reads stdin to completion and discards it. SessionStart input
 * carries `source` and the common fields, but this hook does not
 * branch on them.
 */
function drainStdin(): Promise<string> {
    return new Promise((resolve) => {
        let buf = '';
        process.stdin.on('data', (chunk) => {
            buf += String(chunk);
        });
        process.stdin.on('end', () => resolve(buf));
        process.stdin.on('error', () => resolve(buf));
    });
}

/**
 * Strips a leading YAML frontmatter block (--- ... ---) if present,
 * returning the markdown body only.
 */
function stripFrontmatter(text: string): string {
    if (!text.startsWith('---')) {
        return text;
    }
    const end = text.indexOf('\n---', 3);
    if (end === -1) {
        return text;
    }
    const after = text.indexOf('\n', end + 4);
    return after === -1 ? '' : text.slice(after + 1);
}

function readBody(fileName: string): string | null {
    const fullPath = path.join(INSTRUCTIONS_DIR, fileName);
    try {
        const raw = fs.readFileSync(fullPath, 'utf8');
        return stripFrontmatter(raw).trim();
    } catch (err) {
        const message = err instanceof Error ? err.message : String(err);
        process.stderr.write(
            `[autocontext-session-start] could not read ${fullPath}: ${message}\n`,
        );
        return null;
    }
}

async function main(): Promise<void> {
    await drainStdin();

    const sections: string[] = [];
    for (const fileName of FILES) {
        const body = readBody(fileName);
        if (body) {
            sections.push(
                `<!-- injected by AutoContext SessionStart hook from ${fileName} -->\n\n${body}`,
            );
        }
    }

    if (sections.length === 0) {
        // Nothing to inject. Emit a no-op success so the session continues.
        process.stdout.write('{}\n');
        return;
    }

    const additionalContext = [
        '# AutoContext meta-instructions (injected at session start)',
        '',
        'These are the always-attached instruction files shipped with the AutoContext VS Code extension. They are injected here, in addition to the chatInstructions manifest entries, to ensure the rules below are applied to every turn — including ones the agent might otherwise treat as trivial.',
        '',
        sections.join('\n\n---\n\n'),
    ].join('\n');

    const output: SessionStartOutput = {
        hookSpecificOutput: {
            hookEventName: 'SessionStart',
            additionalContext,
        },
    };

    process.stdout.write(`${JSON.stringify(output)}\n`);
}

void main();
