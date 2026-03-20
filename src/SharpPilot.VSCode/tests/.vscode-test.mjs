import { defineConfig } from '@vscode/test-cli';
import { fileURLToPath } from 'node:url';
import { resolve, dirname } from 'node:path';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');

export default defineConfig({
    files: '../out/tests/smoke-tests/**/*.test.js',
    extensionDevelopmentPath: root,
    mocha: { timeout: 10_000 },
    launchArgs: ['--disable-extensions'],
});
