import type { InstructionsFileSection } from './instructions-file-section.js';

/**
 * Runtime-only section descriptor that augments the catalog shape
 * (`InstructionsFileSection`) with the body-relative character offsets
 * `[charStart, charEnd)` for the section's slice.
 *
 * Build-time JSON deliberately stores only the catalog fields; offsets
 * are recomputed at runtime against the body actually being read so
 * they remain correct after user-driven content filtering (e.g.
 * `disabledInstructions`) shifts the bytes around.
 */
export interface InstructionsFileSectionWithOffsets extends InstructionsFileSection {
    readonly charStart: number;
    readonly charEnd: number;
}
