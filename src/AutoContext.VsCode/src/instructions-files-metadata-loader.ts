import type { InstructionsFileMetadata } from '#types/instructions-file-metadata.js';
import type { InstructionsFilesMetadata } from '#types/instructions-files-metadata.js';
import { ResourceManifestLoader } from './resource-manifest-loader.js';

/**
 * Reads `resources/instructions-files.metadata.json` (the build-time
 * artifact produced by `instructions-files-metadata-generator.ts`) and
 * projects it into a map keyed by file name (e.g.
 * `lang-csharp.instructions.md`) for `InstructionsFilesManifestLoader`
 * to consume.
 *
 * Replaces the runtime markdown re-parser previously implemented by
 * `InstructionsFileMetadataReader`.
 */
export class InstructionsFilesMetadataLoader
    extends ResourceManifestLoader<InstructionsFilesMetadata, ReadonlyMap<string, InstructionsFileMetadata>> {

    constructor(extensionPath: string) {
        super(extensionPath, 'instructions-files.metadata.json');
    }

    protected project(json: InstructionsFilesMetadata): ReadonlyMap<string, InstructionsFileMetadata> {
        if (!Array.isArray(json.instructions)) {
            this.fail("missing 'instructions' array.");
        }

        const map = new Map<string, InstructionsFileMetadata>();
        for (const entry of json.instructions) {
            map.set(entry.fileName, {
                description: entry.description,
                version: entry.version,
                hasChangelog: entry.hasChangelog,
            });
        }
        return map;
    }
}
