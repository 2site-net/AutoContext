import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

export default defineConfig({
    test: {
        include: ['tests/**/*.test.ts'],
        alias: {
            '#src/': fileURLToPath(new URL('./src/', import.meta.url)),
        },
    },
});
