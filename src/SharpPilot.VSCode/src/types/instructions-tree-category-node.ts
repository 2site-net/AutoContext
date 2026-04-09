import type { InstructionsTreeNode } from './instructions-tree-node.js';

export interface InstructionsTreeCategoryNode {
    readonly kind: 'category';
    readonly name: string;
    readonly children: readonly InstructionsTreeNode[];
}
