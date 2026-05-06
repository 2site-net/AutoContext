/**
 * Glob-vs-glob set-intersection check that mirrors how VS Code
 * decides which `chatInstructions` attach to a file. Implemented in
 * Step 5; consumed by the metadata predicate (Step 4) and by the
 * content-search handler (Step 7) for `applyTo` post-filtering.
 */
export interface InstructionsFilesLmToolsApplyToMatcher {
    /**
     * Returns true iff at least one workspace file matches both
     * `userInputGlob` (the user's query glob or concrete path) and
     * `instructionApplyTo` (the instruction file's own `applyTo`
     * glob).
     */
    matches(userInputGlob: string, instructionApplyTo: string): Promise<boolean>;
}
