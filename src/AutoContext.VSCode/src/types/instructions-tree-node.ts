import type { InstructionsCatalogEntry } from '../instructions-catalog-entry.js';
import type { TreeViewNodeState } from '../tree-view-node-state.js';

export interface InstructionsTreeNode {
    readonly kind: 'instructions';
    readonly entry: InstructionsCatalogEntry;
    readonly state: TreeViewNodeState;
    readonly overrideVersion?: string;
    readonly isOutdated: boolean;
}
