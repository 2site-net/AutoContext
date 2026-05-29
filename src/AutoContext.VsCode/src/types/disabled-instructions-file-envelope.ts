/**
 * Minimal envelope returned from `get_autocontext_instructions_file`
 * when the requested file is currently disabled (per-file activation
 * flags unmet, or user toggled it off in `autocontext.json`). Carries
 * identity only — no `content`, no `description`, no `enabled` flag —
 * so the model cannot route around the user's choice by quoting back
 * the muted rule.
 */
export interface DisabledInstructionsFileEnvelope {
    readonly name: string;
    readonly key: string;
    readonly disabled: true;
}
