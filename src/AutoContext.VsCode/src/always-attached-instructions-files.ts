// Files that ship with the AutoContext extension and are always
// attached to every chat turn. They live under `instructions/` like the
// curated rule files but are intentionally NOT listed in
// `resources/instructions-files.json`, so the manifest loader (and
// therefore the tree view, diagnostics runner, and disable mechanics)
// never sees them. The package-instructions-manifest emits them as
// unconditional `chatInstructions` entries; the metadata generator
// exempts them from the curated cross-check.
//
// Order matters: it determines the order of the corresponding
// `chatInstructions` entries in `package.json`.
export const ALWAYS_ATTACHED_INSTRUCTIONS_FILES = [
    'copilot.instructions.md',
    'autocontext.instructions.md',
] as const;

export const ALWAYS_ATTACHED_INSTRUCTIONS_FILES_SET: ReadonlySet<string> =
    new Set(ALWAYS_ATTACHED_INSTRUCTIONS_FILES);
