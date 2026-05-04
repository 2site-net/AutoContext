import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { InstructionsFilesMetadataLoader } from '#src/instructions-files-metadata-loader';
import type { InstructionsFilesMetadata } from '#types/instructions-files-metadata.js';

describe('InstructionsFilesMetadataLoader', () => {
    let root: string;
    let resourcesDir: string;

    beforeEach(() => {
        root = mkdtempSync(join(tmpdir(), 'autocontext-instructions-files-metadata-loader-'));
        resourcesDir = join(root, 'resources');
        mkdirSync(resourcesDir, { recursive: true });
    });

    afterEach(() => {
        rmSync(root, { recursive: true, force: true });
    });

    function writeMetadata(metadata: InstructionsFilesMetadata): void {
        writeFileSync(
            join(resourcesDir, 'instructions-files.metadata.json'),
            JSON.stringify(metadata, null, 2) + '\n',
        );
    }

    it('projects metadata into a map keyed by file name', () => {
        writeMetadata({
            schemaVersion: '1',
            instructions: [
                {
                    key: 'alpha',
                    fileName: 'alpha.instructions.md',
                    name: 'alpha (v1.2.3)',
                    version: '1.2.3',
                    description: 'Alpha desc',
                    hasChangelog: true,
                    contentHash: 'sha256:aaa',
                    sections: [],
                },
                {
                    key: 'beta',
                    fileName: 'beta.instructions.md',
                    name: 'beta (v0.1.0)',
                    version: '0.1.0',
                    description: 'Beta desc',
                    hasChangelog: false,
                    contentHash: 'sha256:bbb',
                    sections: [],
                },
            ],
        });

        const map = new InstructionsFilesMetadataLoader(root).load();

        expect(map.size).toBe(2);
        expect(map.get('alpha.instructions.md')).toEqual({
            description: 'Alpha desc',
            version: '1.2.3',
            hasChangelog: true,
        });
        expect(map.get('beta.instructions.md')).toEqual({
            description: 'Beta desc',
            version: '0.1.0',
            hasChangelog: false,
        });
    });

    it('returns an empty map for an empty manifest', () => {
        writeMetadata({ schemaVersion: '1', instructions: [] });

        const map = new InstructionsFilesMetadataLoader(root).load();

        expect(map.size).toBe(0);
    });

    it("fails when the 'instructions' array is missing", () => {
        writeFileSync(
            join(resourcesDir, 'instructions-files.metadata.json'),
            JSON.stringify({ schemaVersion: '1' }) + '\n',
        );

        expect(() => new InstructionsFilesMetadataLoader(root).load()).toThrow(
            /instructions-files\.metadata\.json: missing 'instructions' array/,
        );
    });

    it('surfaces JSON parse failures with file context', () => {
        writeFileSync(join(resourcesDir, 'instructions-files.metadata.json'), '{ not json');

        expect(() => new InstructionsFilesMetadataLoader(root).load()).toThrow(
            /instructions-files\.metadata\.json: failed to parse JSON/,
        );
    });
});
