/**
 * One excerpt from a content-search hit. Excerpts always come from
 * the body and carry section attribution derived from the section
 * index's `[charStart, charEnd)` offsets.
 *
 * `sectionLevel` is `2` for top-level (`##`) sections and `3` for
 * nested (`###`) sections, mirroring `InstructionsFileSection.parent`
 * (level = `parent ? 3 : 2`).
 */
export interface InstructionsFilesLmToolsContentExcerpt {
    readonly text: string;
    readonly section: string;
    readonly sectionLevel: 2 | 3;
    readonly anchor: string;
}
