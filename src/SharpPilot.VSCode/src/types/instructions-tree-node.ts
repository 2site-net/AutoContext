import type { InstructionsCatalogEntry } from '../instructions-catalog-entry.js';
import type { TreeViewNodeState } from '../ui-constants.js';

export interface InstructionsTreeNode {
    readonly kind: 'instructions';
    readonly entry: InstructionsCatalogEntry;
    readonly state: TreeViewNodeState;
}
