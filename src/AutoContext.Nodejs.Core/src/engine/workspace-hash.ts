import { createHash } from 'node:crypto';
import { platform } from 'node:os';
import { resolve } from 'node:path';

/** Character count of the `<workspaceHash>` endpoint segment. */
export const WORKSPACE_HASH_LENGTH = 16;

/**
 * Derives the 16-uppercase-hex `<workspaceHash>` endpoint segment for
 * {@link workspacePath}: the leading hex characters of the SHA-256 of
 * the normalised absolute path.
 *
 * Contract counterpart of the C# `WorkspaceHash.Compute` in
 * `AutoContext.Engine.Protocol`; the two must agree character for
 * character or a TypeScript client dials a pipe no engine bound.
 *
 * @throws When {@link workspacePath} is blank.
 */
export function computeWorkspaceHash(workspacePath: string): string {
    if (workspacePath.trim() === '') {
        throw new Error('workspacePath must not be blank.');
    }

    const digest = createHash('sha256')
        .update(normalise(workspacePath), 'utf8')
        .digest('hex');

    return digest.slice(0, WORKSPACE_HASH_LENGTH).toUpperCase();
}

/**
 * Resolves the path, drops trailing separators, and folds case on
 * Windows so equivalent spellings of one workspace hash identically.
 */
function normalise(workspacePath: string): string {
    const full = resolve(workspacePath);

    // Preserve the root segment so 'C:\' doesn't collapse to 'C:' on
    // Windows or '/' to '' on POSIX after trimming.
    const trimmed = trimTrailingSeparators(full);
    const preserved = trimmed.length === 0 ? full : trimmed;

    return platform() === 'win32' ? preserved.toUpperCase() : preserved;
}

function trimTrailingSeparators(value: string): string {
    let end = value.length;
    while (end > 0 && (value[end - 1] === '\\' || value[end - 1] === '/')) {
        end -= 1;
    }
    return value.slice(0, end);
}
