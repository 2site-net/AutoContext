import type { InstructionsFileSectionWithOffsets } from './instructions-file-section-with-offsets.js';

/**
 * Projection of an instruction file ready for LM-tool consumption:
 * the markdown body to surface and its parsed section index.
 */
export interface InstructionsFileProjection {
    readonly body: string;
    readonly sections: readonly InstructionsFileSectionWithOffsets[];
}
