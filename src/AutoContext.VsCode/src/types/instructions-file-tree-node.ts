import type { InstructionsFileEntry } from '../instructions-file-entry.js';
import type { TreeViewNodeState } from '../tree-view-node-state.js';

export interface InstructionsFileTreeNode {
    readonly kind: 'instructions';
    readonly entry: InstructionsFileEntry;
    readonly state: TreeViewNodeState;
    readonly overrideVersion?: string;
    readonly isOutdated: boolean;
}
