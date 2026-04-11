// Reads per-project .mcp-tools.json files, merges them into a single manifest
// keyed by server category, validates against the ui-constants catalog, and
// writes the result to .mcp-tools.json at the extension root.
// Self-executable: tsx src/mcp-tools-manifest.ts
//
// Input / output shape (per server category):
//   { "<serverCategory>": [ { "name", "description", "version", "features"?: [...] } ] }

import { existsSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { mcpTools } from './ui-constants.js';

interface McpToolFeature {
    name: string;
    description: string;
    version: string;
}

interface McpToolEntry {
    name: string;
    description: string;
    version: string;
    features?: McpToolFeature[];
}

type McpToolsManifest = Record<string, McpToolEntry[]>;

function findProjectManifests(root: string, exclude: string): string[] {
    return readdirSync(root, { withFileTypes: true })
        .filter(e => e.isDirectory() && join(root, e.name) !== exclude)
        .map(e => join(root, e.name, '.mcp-tools.json'))
        .filter(p => existsSync(p));
}

export function mergeManifests(root: string, exclude: string): McpToolsManifest {
    const merged: McpToolsManifest = {};

    for (const fullPath of findProjectManifests(root, exclude)) {
        const content: McpToolsManifest = JSON.parse(readFileSync(fullPath, 'utf-8'));

        for (const [category, tools] of Object.entries(content)) {
            if (merged[category]) {
                throw new Error(`Duplicate server category '${category}' found in ${fullPath}. Each category must belong to a single project.`);
            }

            merged[category] = tools;
        }
    }

    return merged;
}

export function validate(manifest: McpToolsManifest): void {
    // Collect all leaf keys: feature names from composite tools, tool keys from standalone tools.
    const leafKeys = new Set<string>();
    // Collect composite tool names for cross-checking against toolName references.
    const compositeToolNames = new Set<string>();

    for (const tools of Object.values(manifest)) {
        for (const tool of tools) {
            if (tool.features) {
                compositeToolNames.add(tool.name);
                for (const feature of tool.features) {
                    if (leafKeys.has(feature.name)) {
                        throw new Error(`Duplicate feature name '${feature.name}' in manifest.`);
                    }
                    leafKeys.add(feature.name);
                }
            } else {
                if (leafKeys.has(tool.name)) {
                    throw new Error(`Duplicate tool name '${tool.name}' in manifest.`);
                }
                leafKeys.add(tool.name);
            }
        }
    }

    const catalogKeys = new Set(mcpTools.map(t => t.key));
    const catalogToolNames = new Set(mcpTools.map(t => t.toolName).filter((n): n is string => n !== undefined));

    const errors: string[] = [];

    const missingInManifest = [...catalogKeys].filter(k => !leafKeys.has(k));
    const missingInCatalog = [...leafKeys].filter(n => !catalogKeys.has(n));

    if (missingInManifest.length > 0) {
        errors.push(`Tools in ui-constants but missing from .mcp-tools.json: ${missingInManifest.join(', ')}`);
    }
    if (missingInCatalog.length > 0) {
        errors.push(`Tools in .mcp-tools.json but missing from ui-constants: ${missingInCatalog.join(', ')}`);
    }

    const missingComposites = [...catalogToolNames].filter(n => !compositeToolNames.has(n));
    const extraComposites = [...compositeToolNames].filter(n => !catalogToolNames.has(n));

    if (missingComposites.length > 0) {
        errors.push(`Composite tools referenced in ui-constants but missing from .mcp-tools.json: ${missingComposites.join(', ')}`);
    }
    if (extraComposites.length > 0) {
        errors.push(`Composite tools in .mcp-tools.json but not referenced in ui-constants: ${extraComposites.join(', ')}`);
    }

    if (errors.length > 0) {
        throw new Error(`MCP tools manifest validation failed:\n  ${errors.join('\n  ')}`);
    }
}

if (process.argv[1]?.replace(/\\/g, '/').endsWith('/src/mcp-tools-manifest.ts')) {
    const extensionRoot = join(dirname(fileURLToPath(import.meta.url)), '..');
    const repoSrcDir = join(extensionRoot, '..');
    const outputPath = join(extensionRoot, '.mcp-tools.json');

    const manifest = mergeManifests(repoSrcDir, extensionRoot);
    validate(manifest);

    writeFileSync(outputPath, JSON.stringify(manifest, null, 2) + '\n', 'utf-8');

    const toolCount = Object.values(manifest)
        .reduce((sum, tools) => sum + tools.length, 0);
    const categories = Object.keys(manifest).join(', ');
    console.log(`Generated MCP tools manifest: ${toolCount} tools across [${categories}].`);
}
