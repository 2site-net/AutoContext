import type { InstructionsFileSection } from './instructions-file-section.js';

/**
 * Optional instructions-files metadata loaded at runtime, keyed by
 * file name. Includes the build-time description, version, changelog
 * presence, `applyTo` glob (when frontmatter declares one), and the
 * catalog-only section index. Consumed by `InstructionsFileEntry`
 * (description / version / hasChangelog only) and by
 * `InstructionsFilesLmToolsMetadataViews` (full surface).
 */
export interface InstructionsFileMetadata {
    readonly description?: string;
    readonly version?: string;
    readonly hasChangelog?: boolean;
    readonly applyTo?: string;
    readonly sections?: readonly InstructionsFileSection[];
}
