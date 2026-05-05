import * as vscode from 'vscode';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import type { ChannelLogger } from 'autocontext-framework-web';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import { InstructionsRulesUtils } from './instructions-rules-utils.js';

/**
 * Reads an instruction file's authored markdown body for LM-tool surfaces.
 *
 * - When the workspace ships an override under `.github/instructions/<fileName>`
 *   (as reported by `WorkspaceContextDetector.hasOverriddenFile`), the override
 *   body wins.
 * - Otherwise the bundled `instructions/<fileName>` is read.
 *
 * The result has frontmatter and `[INSTxxxx]` tags stripped. Bullet-level
 * `disabledInstructions` are intentionally **not** applied — LM tools surface
 * the rules as authored. The `instructions/.generated/` tree is also bypassed
 * for the same reason.
 */
export class InstructionsContentProjector {
    private static readonly frontmatterStripPattern = /^---\r?\n[\s\S]*?\r?\n---\r?\n?/;
    private static readonly utf8Decoder = new TextDecoder();

    constructor(
        private readonly extensionPath: string,
        private readonly detector: WorkspaceContextDetector,
        private readonly logger: ChannelLogger,
    ) {}

    async project(fileName: string): Promise<string | undefined> {
        const raw = await this.readSource(fileName);
        if (raw === undefined) {
            return undefined;
        }
        const stripped = raw.replace(InstructionsContentProjector.frontmatterStripPattern, '');
        return InstructionsRulesUtils.stripAllRulesIds(stripped);
    }

    private async readSource(fileName: string): Promise<string | undefined> {
        if (this.detector.hasOverriddenFile(fileName)) {
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
            return InstructionsContentProjector.utf8Decoder.decode(bytes);
        } catch (err) {
            this.logger.warn(`Failed to read override for ${fileName}; falling back to bundled`, err);
            return undefined;
        }
    }

    private async readBundled(fileName: string): Promise<string | undefined> {
        const path = join(this.extensionPath, 'instructions', fileName);
        try {
            return await readFile(path, 'utf-8');
        } catch (err) {
            this.logger.warn(`Failed to read bundled instruction file: ${fileName}`, err);
            return undefined;
        }
    }
}
