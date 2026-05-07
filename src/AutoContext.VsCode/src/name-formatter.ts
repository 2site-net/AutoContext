/**
 * Centralizes the conventions used to derive human-facing labels from
 * an AutoContext binary's full assembly name (e.g.
 * `AutoContext.Worker.DotNet`).
 *
 * Kept separate from `ServerEntry` so the formatting rules have a
 * single, name-focused home and can be applied to any string source —
 * including ones that don't have a `ServerEntry` instance to hand
 * (e.g. a worker greeting payload).
 */
export class NameFormatter {
    private static readonly prefix = 'AutoContext.';
    private static readonly displayPrefix = 'AutoContext: ';

    /**
     * Strips the `AutoContext.` package prefix.
     *
     * `"AutoContext.Worker.DotNet"` → `"Worker.DotNet"`.
     */
    static toShortName(fullName: string): string {
        return fullName.startsWith(NameFormatter.prefix)
            ? fullName.slice(NameFormatter.prefix.length)
            : fullName;
    }

    /**
     * Converts the package prefix to the user-facing display prefix.
     *
     * `"AutoContext.Worker.DotNet"` → `"AutoContext: Worker.DotNet"`.
     * Used as the canonical output-channel name for a worker.
     */
    static toDisplayName(fullName: string): string {
        return NameFormatter.displayPrefix + NameFormatter.toShortName(fullName);
    }
}
