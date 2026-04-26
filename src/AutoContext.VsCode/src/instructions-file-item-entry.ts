/**
 * Common base for any item in the instructions-files manifest that has
 * a name and (optionally) a description. Concrete subclasses are
 * `InstructionsFileCategoryEntry` and `InstructionsFileEntry`.
 */
export abstract class InstructionsFileItemEntry {
    protected constructor(
        readonly name: string,
        readonly description?: string,
    ) {}
}
