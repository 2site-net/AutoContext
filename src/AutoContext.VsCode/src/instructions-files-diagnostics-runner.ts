import { join } from 'node:path';
import type { InstructionsFilesManifest } from './instructions-files-manifest.js';
import type { InstructionsFileEntry } from './instructions-file-entry.js';
import { InstructionsFileParser } from './instructions-file-parser.js';
import type { AutoContextConfigManager } from './autocontext-config-manager.js';
import type { InstructionsFilesDiagnosticRecord } from '#types/instructions-files-diagnostic-record.js';

/**
 * Walks the instructions manifest, parses each file, applies the active
 * AutoContext config filter (`diagnostic.warnOnMissingId`), and returns a
 * flat list of diagnostic records. Pure data — callers decide how to
 * surface them (output channel, problems panel, tree decorations, etc.).
 *
 * Per-file parsing runs concurrently via `Promise.all`; the parser
 * caches by mtime so repeat calls are cheap, but the cold first run
 * benefits from the parallel fan-out.
 */
export class InstructionsFilesDiagnosticsRunner {
    constructor(
        private readonly extensionPath: string,
        private readonly configManager: AutoContextConfigManager,
        private readonly manifest: InstructionsFilesManifest,
    ) {}

    async collect(): Promise<InstructionsFilesDiagnosticRecord[]> {
        const config = await this.configManager.read();
        const warnOnMissingId = config.diagnostic?.warnOnMissingId === true;

        const perFile = await Promise.all(
            this.manifest.instructions.map(entry =>
                this.collectForEntry(entry, warnOnMissingId),
            ),
        );

        return perFile.flat();
    }

    private async collectForEntry(
        entry: InstructionsFileEntry,
        warnOnMissingId: boolean,
    ): Promise<InstructionsFilesDiagnosticRecord[]> {
        let diagnostics;

        try {
            ({ result: { diagnostics } } = await InstructionsFileParser.fromFile(
                join(this.extensionPath, 'instructions', entry.name),
            ));
        } catch (err) {
            return [{
                kind: 'parse-error',
                entry: entry.name,
                message: err instanceof Error ? err.message : String(err),
            }];
        }

        const records: InstructionsFilesDiagnosticRecord[] = [];

        for (const d of diagnostics) {
            if (d.kind === 'missing-id' && !warnOnMissingId) {
                continue;
            }

            records.push({
                kind: d.kind,
                entry: entry.name,
                line: d.line,
                message: d.message,
            });
        }

        return records;
    }
}
