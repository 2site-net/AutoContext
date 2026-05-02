import { InstructionsFileCategoryEntry } from './instructions-file-category-entry.js';
import { InstructionsFileEntry } from './instructions-file-entry.js';
import type { InstructionsFileMetadata } from '#types/instructions-file-metadata.js';
import { InstructionsFilesManifest } from './instructions-files-manifest.js';
import { ResourceManifestLoader } from './resource-manifest-loader.js';

interface JsonInstructionsFile {
    label: string;
    name: string;
    categories: readonly string[];
    activationFlags?: readonly string[];
}

interface JsonInstructionsCategory {
    name: string;
    description?: string;
}

interface JsonInstructionsFilesManifest {
    schemaVersion?: string;
    categories: readonly JsonInstructionsCategory[];
    instructions: readonly JsonInstructionsFile[];
}

/**
 * Loads `resources/instructions-files.json` from the extension folder
 * and projects it into a fully-resolved `InstructionsFilesManifest`
 * instance. Optionally enriches each entry with metadata (description,
 * version, changelog presence) keyed by file name.
 */
export class InstructionsFilesManifestLoader
    extends ResourceManifestLoader<JsonInstructionsFilesManifest, InstructionsFilesManifest> {

    private metadata: ReadonlyMap<string, InstructionsFileMetadata> | undefined;

    constructor(extensionPath: string) {
        super(extensionPath, 'instructions-files.json');
    }

    override load(metadata?: ReadonlyMap<string, InstructionsFileMetadata>): InstructionsFilesManifest {
        this.metadata = metadata;
        try {
            return super.load();
        } finally {
            this.metadata = undefined;
        }
    }

    protected project(json: JsonInstructionsFilesManifest): InstructionsFilesManifest {
        const categories = json.categories.map(c =>
            new InstructionsFileCategoryEntry(c.name, c.description),
        );
        const categoryByName = new Map<string, InstructionsFileCategoryEntry>(
            categories.map(c => [c.name, c]),
        );

        const instructions = json.instructions.map(i => {
            this.validateCategories(i.name, i.categories, categoryByName);
            const entryCategories = i.categories.map(name => categoryByName.get(name)!);
            return new InstructionsFileEntry(
                i.name,
                i.label,
                entryCategories,
                i.activationFlags,
                this.metadata?.get(i.name),
            );
        });

        return new InstructionsFilesManifest(instructions, categories);
    }

    private validateCategories(
        name: string,
        categoryNames: readonly string[],
        categoryByName: ReadonlyMap<string, InstructionsFileCategoryEntry>,
    ): void {
        if (categoryNames.length === 0) {
            this.fail(`instruction '${name}' has no categories.`);
        }

        for (const c of categoryNames) {
            if (!categoryByName.has(c)) {
                this.fail(`instruction '${name}' references unknown category '${c}'.`);
            }
        }
    }
}
