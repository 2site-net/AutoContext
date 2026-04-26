import { InstructionsFileItemEntry } from './instructions-file-item-entry.js';

/**
 * A category from `resources/instructions-files.json`. Categories
 * provide grouping for instructions in the tree view; their order
 * in the manifest dictates render order.
 */
export class InstructionsFileCategoryEntry extends InstructionsFileItemEntry {
    constructor(name: string, description?: string) {
        super(name, description);
    }
}
