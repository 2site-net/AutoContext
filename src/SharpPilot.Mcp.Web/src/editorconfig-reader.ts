import { parseSync } from 'editorconfig';

/**
 * Resolves the effective `.editorconfig` properties for a given file path.
 * Walks up the directory tree, evaluates glob patterns and section cascading,
 * and returns the final resolved key-value pairs formatted as a human-readable
 * report.
 */
export function read(path: string): string {
    if (!path.trim()) {
        throw new Error('File path must not be empty or whitespace.');
    }

    const properties = parseSync(path);

    const entries = Object.entries(properties).filter(
        ([key]) => key !== 'tab_width',
    );

    if (entries.length === 0) {
        return '⚠️ No .editorconfig properties apply to this file.';
    }

    return entries.map(([key, value]) => `${key} = ${String(value)}`).join('\n');
}

/**
 * Resolves the effective `.editorconfig` properties for a given file path
 * as a record for programmatic use by checkers.
 *
 * Returns `undefined` when the path is empty or no properties apply.
 */
export function resolve(path: string | undefined): Record<string, string> | undefined {
    if (!path?.trim()) {
        return undefined;
    }

    const properties = parseSync(path);

    const entries = Object.entries(properties).filter(
        ([key]) => key !== 'tab_width',
    );

    if (entries.length === 0) {
        return undefined;
    }

    const result: Record<string, string> = {};

    for (const [key, value] of entries) {
        result[key] = String(value);
    }

    return result;
}
