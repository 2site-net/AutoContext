import type { InstructionsFilesManifest } from './instructions-files-manifest.js';
import type { InstructionsFileMetadata } from './types/instructions-file-metadata.js';
import type { InstructionsFilesLmToolsMetadataView } from './types/instructions-files-lm-tools-metadata-view.js';

/**
 * Assembles the flattened
 * `InstructionsFilesLmToolsMetadataView` rows consumed by the
 * metadata-predicate engine and the LM-tool handlers from the runtime
 * manifest (categories, label) joined with the build-time metadata
 * map (description, version, applyTo, sections).
 *
 * Cached on first access — the corpus is fixed for the lifetime of
 * the extension host (overrides only affect *body* content, not
 * metadata-frontmatter fields surfaced here).
 */
export class InstructionsFilesLmToolsMetadataViews {
    #cached: readonly InstructionsFilesLmToolsMetadataView[] | undefined;

    constructor(
        private readonly manifest: InstructionsFilesManifest,
        private readonly metadata: ReadonlyMap<string, InstructionsFileMetadata>,
    ) {}

    getAll(): readonly InstructionsFilesLmToolsMetadataView[] {
        return (this.#cached ??= this.build());
    }

    private build(): readonly InstructionsFilesLmToolsMetadataView[] {
        const out: InstructionsFilesLmToolsMetadataView[] = [];
        for (const entry of this.manifest.instructions) {
            const meta = this.metadata.get(entry.name);
            out.push({
                name: entry.name,
                key: entry.key,
                fileName: entry.name,
                description: entry.description ?? '',
                version: entry.version ?? '',
                ...(meta?.applyTo !== undefined ? { applyTo: meta.applyTo } : {}),
                hasChangelog: entry.hasChangelog,
                categories: entry.categories.map(c => c.name),
                sections: meta?.sections ?? [],
            });
        }
        return out;
    }
}
