import { readFileSync } from 'node:fs';

/**
 * Utility helpers for parsing JSON content from disk with
 * contextualised error messages.
 */
export class JsonFile {
    /**
     * Reads a UTF-8 file and parses it as JSON. Wraps `JSON.parse`
     * failures with a contextualised message that includes the file
     * path, so a corrupted resource fails loudly instead of crashing
     * activation with a bare `SyntaxError` and no indication of which
     * file was at fault.
     */
    static fromUtf8<T>(path: string): T {
        const raw = readFileSync(path, 'utf-8');
        try {
            return JSON.parse(raw) as T;
        } catch (err) {
            const reason = err instanceof Error ? err.message : String(err);
            throw new Error(`Failed to parse JSON from '${path}': ${reason}`);
        }
    }
}
