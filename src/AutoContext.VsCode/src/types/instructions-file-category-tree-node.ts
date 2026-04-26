import type { InstructionsFileTreeNode } from './instructions-file-tree-node.js';

export interface InstructionsFileCategoryTreeNode {
    readonly kind: 'categoryNode';
    readonly name: string;
    readonly children: readonly InstructionsFileTreeNode[];
}
