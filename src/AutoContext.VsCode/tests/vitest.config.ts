import { defineConfig } from 'vitest/config';

export default defineConfig({
    test: {
        include: ['tests/unit-tests/**/*.test.ts'],
        exclude: ['**/node_modules/**', 'dist/**'],
        alias: {
            vscode: new URL('unit-tests/_fakes/fake-vscode.ts', import.meta.url).pathname,
            '#types/': new URL('../src/types/', import.meta.url).pathname,
        },
    },
});
