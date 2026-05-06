import * as vscode from 'vscode';
import type { InstructionsFileContentProjector } from './instructions-file-content-projector.js';
import type { InstructionsFileEntry } from './instructions-file-entry.js';
import type { InstructionsFilesManifest } from './instructions-files-manifest.js';
import type { InstructionsFileSectionWithOffsets } from './types/instructions-file-section-with-offsets.js';
import type { InstructionsFilesLmToolsGetResult } from './types/instructions-files-lm-tools-get-result.js';

/**
 * Get-tool input. `sections` is the chained input from the other
 * three tools (`matchedAnchors` from `_by_metadata`, `excerpts[].anchor`
 * from `_by_content`); a non-empty array narrows the response to the
 * named sections only. `name` is the full instructions filename
 * (e.g. `lang-csharp.instructions.md`).
 */
export interface InstructionsFilesLmToolsGetInput {
    readonly name: string;
    readonly sections?: readonly string[];
}

/**
 * Powers `get_autocontext_instructions_file`. Lookup by exact `name`
 * against the bundled manifest:
 *
 * - **Not bundled** → `{ kind: 'not-found', name }`. The platform's
 *   tool-call already echoed the name to the model, so no identity
 *   leak — but no body either.
 * - **Disabled** (per-file activation flag unmet, or
 *   `autocontext.json` toggle off) →
 *   `DisabledInstructionsFileEnvelope { name, key, disabled: true }`.
 *   Identity-only; no body, no description, no version.
 * - **Active** → projector body (override-aware). Section-scoped
 *   slicing concatenates `body.slice(charStart, charEnd)` for each
 *   matched anchor in document order; unknown anchors flow back as
 *   `notFoundSections`. Empty `sections` array ⇒ same as omitted
 *   ⇒ whole body returned.
 */
export class InstructionsFilesLmToolsGetHandler
    implements vscode.LanguageModelTool<InstructionsFilesLmToolsGetInput> {

    constructor(
        private readonly manifest: InstructionsFilesManifest,
        private readonly projector: InstructionsFileContentProjector,
    ) {}

    async invoke(
        options: vscode.LanguageModelToolInvocationOptions<InstructionsFilesLmToolsGetInput>,
        _token: vscode.CancellationToken,
    ): Promise<vscode.LanguageModelToolResult> {
        const result = await this.handle(options.input);
        return new vscode.LanguageModelToolResult([
            new vscode.LanguageModelTextPart(JSON.stringify(result)),
        ]);
    }

    async handle(input: InstructionsFilesLmToolsGetInput): Promise<InstructionsFilesLmToolsGetResult> {
        const entry = this.manifest.findByName(input.name);
        if (!entry) {
            return { kind: 'not-found', name: input.name };
        }
        if (!entry.resolveState().isActive()) {
            return { name: entry.name, key: entry.key, disabled: true };
        }

        const projection = await this.projector.project(entry.name);
        if (!projection) {
            // Body disappeared between manifest and read (rare —
            // override deleted mid-flight, generated wipe). Treat as
            // not-found rather than fabricating empty content.
            return { kind: 'not-found', name: input.name };
        }

        const requested = input.sections;
        if (!requested || requested.length === 0) {
            return {
                kind: 'ok',
                name: entry.name,
                key: entry.key,
                fileName: entry.name,
                content: projection.body,
                returnedSections: projection.sections.map(s => s.anchor),
            };
        }

        return this.sliceSections(entry, projection.body, projection.sections, requested);
    }

    private sliceSections(
        entry: InstructionsFileEntry,
        body: string,
        sections: readonly InstructionsFileSectionWithOffsets[],
        requested: readonly string[],
    ): InstructionsFilesLmToolsGetResult {
        const sectionsByAnchor = new Map(sections.map(s => [s.anchor, s]));
        const matched: InstructionsFileSectionWithOffsets[] = [];
        const notFound: string[] = [];
        const seen = new Set<string>();
        for (const anchor of requested) {
            if (seen.has(anchor)) continue;
            seen.add(anchor);
            const section = sectionsByAnchor.get(anchor);
            if (section) {
                matched.push(section);
            } else {
                notFound.push(anchor);
            }
        }

        // Concatenate slices in document order so the LLM reads the
        // sections in the same flow as the original file, regardless
        // of the order in which the caller listed anchors.
        matched.sort((a, b) => a.charStart - b.charStart);
        const content = matched
            .map(s => body.slice(s.charStart, s.charEnd))
            .join('\n');

        return {
            kind: 'ok',
            name: entry.name,
            key: entry.key,
            fileName: entry.name,
            content,
            returnedSections: matched.map(s => s.anchor),
            ...(notFound.length > 0 ? { notFoundSections: notFound } : {}),
        };
    }
}
