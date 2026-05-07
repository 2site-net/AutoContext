/**
 * Get-tool input. `sections` is the chained input from the other
 * three tools (`matchedAnchors` from `_by_metadata`, `excerpts[].anchor`
 * from `_by_content`); a non-empty array narrows the response to the
 * named sections only. `name` is the full instructions filename
 * (e.g. `lang-csharp.instructions.md`).
 */
export interface InstructionsFilesLmToolsGetInput {
    readonly name: string;
    readonly sections?: readonly string[];
}
