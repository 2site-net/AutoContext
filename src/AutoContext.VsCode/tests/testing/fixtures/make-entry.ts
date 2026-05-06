import { InstructionsFileCategoryEntry } from '../../../src/instructions-file-category-entry';
import { InstructionsFileEntry } from '../../../src/instructions-file-entry';
import type { InstructionsFileMetadata } from '#types/instructions-file-metadata.js';
import { InstructionsFilesManifest } from '../../../src/instructions-files-manifest';
import type { WorkspaceContextDetector } from '../../../src/workspace-context-detector';
import type { InstructionsFilesOverrideWatcher } from '../../../src/instructions-files-override-watcher';
import type { AutoContextConfigManager } from '../../../src/autocontext-config-manager';
import { createFakeDetector, createFakeOverrideWatcher, createFakeConfigManager } from '../fakes';

export function makeInstructionsFileEntry(
    name: string,
    label: string,
    categoryNames: readonly string[],
    activationFlags?: readonly string[],
    metadata?: InstructionsFileMetadata,
    detector: WorkspaceContextDetector = createFakeDetector(),
    configManager: AutoContextConfigManager = createFakeConfigManager(),
    overrideWatcher: InstructionsFilesOverrideWatcher = createFakeOverrideWatcher(),
): InstructionsFileEntry {
    const categories = categoryNames.map(n => new InstructionsFileCategoryEntry(n));
    return new InstructionsFileEntry(name, label, categories, detector, overrideWatcher, configManager, activationFlags, metadata);
}

export function makeInstructionsFilesManifest(
    entries: readonly InstructionsFileEntry[],
): InstructionsFilesManifest {
    const categoryNames = new Set<string>();
    const categories: InstructionsFileCategoryEntry[] = [];
    for (const e of entries) {
        for (const c of e.categories) {
            if (!categoryNames.has(c.name)) {
                categoryNames.add(c.name);
                categories.push(c);
            }
        }
    }
    return new InstructionsFilesManifest(entries, categories);
}
