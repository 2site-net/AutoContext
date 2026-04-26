/**
 * Extension-side runtime concerns for an instructions file: the
 * `when`-clause context keys VS Code uses to gate visibility. The
 * base `contextKey` toggles whether the bundled instruction is
 * active; the `overrideKey` is set when a workspace-local copy
 * under `.github/instructions/` shadows it. Lives separate from
 * manifest data so the entry class stays focused on the manifest
 * shape.
 */
export class InstructionsFileRuntimeInfo {
    constructor(readonly key: string) {}

    get contextKey(): string {
        return `autocontext.instructions.${this.key}`;
    }

    get overrideKey(): string {
        return `autocontext.override.${this.key}`;
    }
}
