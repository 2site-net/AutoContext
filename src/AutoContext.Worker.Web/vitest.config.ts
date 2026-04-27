import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

export default defineConfig({
    test: {
        include: ['tests/**/*.test.ts'],
        alias: {
            '#types/': fileURLToPath(new URL('./src/types/', import.meta.url)),
            '#testing/': fileURLToPath(new URL('./tests/testing/', import.meta.url)),
            '#src/': fileURLToPath(new URL('./src/', import.meta.url)),
        },
    },
});
