import * as vscode from 'vscode';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import type { ChannelLogger } from 'autocontext-nodejs-core';
import type { InstructionsFilesOverrideWatcher } from './instructions-files-override-watcher.js';
import type { InstructionsFilesManager } from './instructions-files-manager.js';
import type { InstructionsFileSectionsCache } from './instructions-file-sections-cache.js';
import type { InstructionsFileProjection } from './types/instructions-file-projection.js';
import { InstructionsRulesUtils } from './instructions-rules-utils.js';

/**
 * Reads an instruction file's markdown body for LM-tool surfaces, paired
 * with its parsed section index.
 *
 * - When the workspace ships an override under `.github/instructions/<fileName>`
 *   (as reported by `InstructionsFilesOverrideWatcher.isOverridden`), the override
 *   wins. The override is authored markdown, so frontmatter and `[INSTxxxx]`
 *   tags are stripped here.
 * - Otherwise the bundled `instructions/.generated/<fileName>` is read. That
 *   path is the per-window output of `InstructionsFilesManager`, which has
 *   already applied user `disabledInstructions`, stripped frontmatter, and
 *   stripped `[INSTxxxx]` tags. Reads block on `manager.flush()` to ensure
 *   any in-flight write has settled before the file is consumed.
 *
 * Section indexes are computed via `InstructionsFileSectionsCache`, which
 * memoizes per body content.
 */
export class InstructionsFileContentProjector {
    private static readonly frontmatterStripPattern = /^---\r?\n[\s\S]*?\r?\n---\r?\n?/;
    private static readonly utf8Decoder = new TextDecoder();

    constructor(
        private readonly extensionPath: string,
        private readonly overrideWatcher: InstructionsFilesOverrideWatcher,
        private readonly manager: InstructionsFilesManager,
        private readonly sectionsCache: InstructionsFileSectionsCache,
        private readonly logger: ChannelLogger,
    ) {}

    async project(fileName: string): Promise<InstructionsFileProjection | undefined> {
        const body = await this.readBody(fileName);
        if (body === undefined) {
            return undefined;
        }
        return { body, sections: this.sectionsCache.get(body) };
    }

    private async readBody(fileName: string): Promise<string | undefined> {
        if (this.overrideWatcher.isOverridden(fileName)) {
            const overrideBody = await this.readOverride(fileName);
            if (overrideBody !== undefined) {
                return overrideBody;
            }
            // Fall through to bundled if override read failed — better to
            // return *some* body than nothing.
        }
        return this.readBundled(fileName);
    }

    private async readOverride(fileName: string): Promise<string | undefined> {
        try {
            const matches = await vscode.workspace.findFiles(
                `.github/instructions/${fileName}`, undefined, 1,
            );
            const uri = matches[0];
            if (!uri) {
                this.logger.debug(`Override expected for ${fileName} but findFiles returned no match; falling back to bundled`);
                return undefined;
            }
            const bytes = await vscode.workspace.fs.readFile(uri);
            const raw = InstructionsFileContentProjector.utf8Decoder.decode(bytes);
            // Overrides are authored markdown — strip frontmatter + `[INSTxxxx]`.
            const stripped = raw.replace(InstructionsFileContentProjector.frontmatterStripPattern, '');
            return InstructionsRulesUtils.stripAllRulesIds(stripped);
        } catch (err) {
            this.logger.warn(`Failed to read override for ${fileName}; falling back to bundled`, err);
            return undefined;
        }
    }

    private async readBundled(fileName: string): Promise<string | undefined> {
        // Block until any pending generated-files write has settled so we
        // never read a partially promoted body.
        await this.manager.flush();
        const path = join(this.extensionPath, 'instructions', '.generated', fileName);
        try {
            // `.generated/` output already has frontmatter and `[INSTxxxx]`
            // tags stripped by the manager — no further normalization here.
            return await readFile(path, 'utf-8');
        } catch (err) {
            this.logger.warn(`Failed to read bundled instruction file: ${fileName}`, err);
            return undefined;
        }
    }
}
