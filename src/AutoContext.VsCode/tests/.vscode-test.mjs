import { defineConfig } from '@vscode/test-cli';
import { fileURLToPath } from 'node:url';
import { resolve, dirname } from 'node:path';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const workspaces = resolve(root, 'tests', 'workspaces');

const shared = {
    extensionDevelopmentPath: root,
    mocha: { timeout: 10_000 },
};

export default defineConfig([
    // General smoke tests — run against the extension workspace itself
    {
        ...shared,
        files: '../dist/tests/smoke-tests/!(workspace-*).test.js',
        launchArgs: ['--disable-extensions', root],
    },
    // Workspace-specific: mixed (TypeScript + .NET + Git)
    {
        ...shared,
        files: '../dist/tests/smoke-tests/workspace-mixed.test.js',
        launchArgs: ['--disable-extensions', resolve(workspaces, 'mixed')],
    },
    // Workspace-specific: TypeScript only
    {
        ...shared,
        files: '../dist/tests/smoke-tests/workspace-typescript.test.js',
        launchArgs: ['--disable-extensions', resolve(workspaces, 'typescript-only')],
    },
    // Workspace-specific: .NET only
    {
        ...shared,
        files: '../dist/tests/smoke-tests/workspace-dotnet.test.js',
        launchArgs: ['--disable-extensions', resolve(workspaces, 'dotnet-only')],
    },
    // Workspace-specific: Web only (JS + package.json)
    {
        ...shared,
        files: '../dist/tests/smoke-tests/workspace-web.test.js',
        launchArgs: ['--disable-extensions', resolve(workspaces, 'web-only')],
    },
    // Workspace-specific: Empty workspace
    {
        ...shared,
        files: '../dist/tests/smoke-tests/workspace-empty.test.js',
        launchArgs: ['--disable-extensions', resolve(workspaces, 'empty')],
    },
]);
