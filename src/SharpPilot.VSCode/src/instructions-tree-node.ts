import type { InstructionsCatalogEntry } from './instructions-catalog-entry.js';
import type { InstructionState } from './ui-constants.js';

export interface InstructionsTreeNode {
    readonly kind: 'instruction';
    readonly entry: InstructionsCatalogEntry;
    readonly state: InstructionState;
}
