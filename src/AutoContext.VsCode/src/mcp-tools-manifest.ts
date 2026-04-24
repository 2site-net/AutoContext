// Reads per-project .mcp-tools.json files, merges them into a single manifest
// keyed by scope, validates against the ui-constants catalog, and
// writes the result to .mcp-tools.json at the extension root.
// Self-executable: tsx src/mcp-tools-manifest.ts
//
// Input / output shape (per scope):
//   { "<scope>": [ { "name", "description", "version", "tasks"?: [...] } ] }

import { existsSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { mcpTools } from './ui-constants.js';
import { SemVer } from './semver.js';

interface McpToolTask {
    name: string;
    description: string;
    version: string;
}

interface McpToolEntry {
    name: string;
    description: string;
    version: string;
    tasks?: McpToolTask[];
}

type McpToolsManifest = Record<string, McpToolEntry[]>;

function findProjectManifests(root: string, exclude: string): string[] {
    // TODO: AutoContext.Mcp.Server declares a duplicate 'dotnet' scope that conflicts
    // with AutoContext.Mcp.DotNet. Exclude it until AutoContext.Mcp.DotNet is deleted
    // as part of the centralized-MCP migration.
    const ignoredDirs = new Set(['AutoContext.Mcp.Server']);
    return readdirSync(root, { withFileTypes: true })
        .filter(e => e.isDirectory()
            && join(root, e.name) !== exclude
            && !ignoredDirs.has(e.name))
        .map(e => join(root, e.name, '.mcp-tools.json'))
        .filter(p => existsSync(p));
}

export function mergeManifests(root: string, exclude: string): McpToolsManifest {
    const merged: McpToolsManifest = {};

    for (const fullPath of findProjectManifests(root, exclude)) {
        const content: McpToolsManifest = JSON.parse(readFileSync(fullPath, 'utf-8'));

        for (const [scope, tools] of Object.entries(content)) {
            if (merged[scope]) {
                throw new Error(`Duplicate scope '${scope}' found in ${fullPath}. Each scope must belong to a single project.`);
            }

            merged[scope] = tools;
        }
    }

    return merged;
}

export function validate(manifest: McpToolsManifest): void {
    // Collect all leaf keys: task names from composite tools, tool keys from standalone tools.
    const leafKeys = new Set<string>();
    // Collect composite tool names for cross-checking against toolName references.
    const compositeToolNames = new Set<string>();

    for (const tools of Object.values(manifest)) {
        for (const tool of tools) {
            if (tool.tasks) {
                compositeToolNames.add(tool.name);
                for (const task of tool.tasks) {
                    if (leafKeys.has(task.name)) {
                        throw new Error(`Duplicate task name '${task.name}' in manifest.`);
                    }
                    leafKeys.add(task.name);
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

    for (const tools of Object.values(manifest)) {
        for (const tool of tools) {
            if (!SemVer.isValid(tool.version)) {
                errors.push(`Invalid version '${tool.version}' on tool '${tool.name}'. Expected semver format (e.g. 1.0.0 or 1.0.0-beta.1).`);
            }
            for (const task of tool.tasks ?? []) {
                if (!SemVer.isValid(task.version)) {
                    errors.push(`Invalid version '${task.version}' on task '${task.name}' (tool '${tool.name}'). Expected semver format (e.g. 1.0.0 or 1.0.0-beta.1).`);
                }
            }
        }
    }

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
    const scopes = Object.keys(manifest).join(', ');
    console.log(`Generated MCP tools manifest: ${toolCount} tools across [${scopes}].`);
}
