import type { InstructionsFilesLmToolsContentHit } from './instructions-files-lm-tools-content-hit.js';

export interface InstructionsFilesLmToolsSearchByContentResult {
    readonly kind: 'ok';
    readonly results: readonly InstructionsFilesLmToolsContentHit[];
}
