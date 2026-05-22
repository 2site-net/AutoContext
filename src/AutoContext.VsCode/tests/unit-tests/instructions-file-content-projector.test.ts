import { describe, it, expect, vi, beforeEach } from 'vitest';
import { readFile } from 'node:fs/promises';

vi.mock('node:fs/promises', () => ({
    readFile: vi.fn(),
}));

import { workspace } from '#support/fake-vscode';
import { createFakeLogger } from '#support/fake-logger';
import { createFakeOverrideWatcher } from '#support/fake-override-watcher';
import { InstructionsFileContentProjector } from '#src/instructions-file-content-projector';
import { InstructionsFileSectionsCache } from '#src/instructions-file-sections-cache';
import type { InstructionsFilesManager } from '#src/instructions-files-manager';

const generatedBody = `# C#

## Heading A

- **Do** use \`var\` for obvious types.

### Subheading A1

- **Don't** use \`async void\`.
`;

const overrideBody = `---
name: "lang-csharp (v1.0.0)"
description: "Override"
---
# Overridden body

- [INST0001] **Do** override the bundled rule.
`;

function createFakeManager(): InstructionsFilesManager {
    return {
        flush: vi.fn(async () => undefined),
    } as unknown as InstructionsFilesManager;
}

describe('InstructionsFileContentProjector', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('should read the bundled .generated file when no override is reported', async () => {
        vi.mocked(readFile).mockResolvedValue(generatedBody);
        const overrideWatcher = createFakeOverrideWatcher();
        const manager = createFakeManager();
        const cache = new InstructionsFileSectionsCache();
        const projector = new InstructionsFileContentProjector(
            '/ext', overrideWatcher, manager, cache, createFakeLogger(),
        );

        const result = await projector.project('lang-csharp.instructions.md');

        expect(overrideWatcher.isOverridden).toHaveBeenCalledWith('lang-csharp.instructions.md');
        expect(workspace.findFiles).not.toHaveBeenCalled();
        expect(manager.flush).toHaveBeenCalledTimes(1);

        // Path must point at .generated/, not the raw instructions/ tree.
        const readPath = vi.mocked(readFile).mock.calls[0][0];
        expect(String(readPath)).toContain('.generated');
        expect(String(readPath)).toContain('lang-csharp.instructions.md');

        // Bundled body is consumed verbatim — manager already stripped.
        expect(result?.body).toBe(generatedBody);

        // Sections come from the cache and match what the parser would produce.
        expect(result?.sections.map(s => s.heading)).toEqual(['Heading A', 'Subheading A1']);
        expect(result?.sections).toBe(cache.get(generatedBody));
    });

    it('should await manager.flush() before reading the bundled file', async () => {
        let flushSettled = false;
        const manager = {
            flush: vi.fn(async () => {
                await Promise.resolve();
                flushSettled = true;
            }),
        } as unknown as InstructionsFilesManager;

        vi.mocked(readFile).mockImplementation(async () => {
            // Asserting at the moment of the read proves ordering.
            expect(flushSettled).toBe(true);
            return generatedBody;
        });

        const projector = new InstructionsFileContentProjector(
            '/ext', createFakeOverrideWatcher(), manager, new InstructionsFileSectionsCache(), createFakeLogger(),
        );
        await projector.project('lang-csharp.instructions.md');

        expect(manager.flush).toHaveBeenCalledTimes(1);
        expect(readFile).toHaveBeenCalledTimes(1);
    });

    it('should read and normalize the override file when the detector reports an override', async () => {
        const overrideWatcher = createFakeOverrideWatcher();
        vi.mocked(overrideWatcher.isOverridden).mockReturnValue(true);
        const manager = createFakeManager();

        const fakeUri = { path: '/ws/.github/instructions/lang-csharp.instructions.md' };
        vi.mocked(workspace.findFiles).mockResolvedValueOnce([fakeUri] as never);
        vi.mocked(workspace.fs.readFile).mockResolvedValueOnce(
            new TextEncoder().encode(overrideBody) as never,
        );

        const projector = new InstructionsFileContentProjector(
            '/ext', overrideWatcher, manager, new InstructionsFileSectionsCache(), createFakeLogger(),
        );
        const result = await projector.project('lang-csharp.instructions.md');

        expect(workspace.findFiles).toHaveBeenCalledWith(
            '.github/instructions/lang-csharp.instructions.md', undefined, 1,
        );
        expect(readFile).not.toHaveBeenCalled();
        expect(manager.flush).not.toHaveBeenCalled();

        // Override body must have frontmatter and `[INSTxxxx]` stripped.
        expect(result?.body).not.toContain('---');
        expect(result?.body).not.toContain('description:');
        expect(result?.body).not.toContain('[INST0001]');
        expect(result?.body).toContain('# Overridden body');
        expect(result?.body).toContain('**Do** override');
    });

    it('should fall back to the bundled .generated file when the override read fails', async () => {
        const overrideWatcher = createFakeOverrideWatcher();
        vi.mocked(overrideWatcher.isOverridden).mockReturnValue(true);
        vi.mocked(workspace.findFiles).mockResolvedValueOnce([] as never);
        vi.mocked(readFile).mockResolvedValue(generatedBody);
        const manager = createFakeManager();

        const projector = new InstructionsFileContentProjector(
            '/ext', overrideWatcher, manager, new InstructionsFileSectionsCache(), createFakeLogger(),
        );
        const result = await projector.project('lang-csharp.instructions.md');

        expect(result?.body).toBe(generatedBody);
        expect(manager.flush).toHaveBeenCalledTimes(1);
        const readPath = vi.mocked(readFile).mock.calls[0][0];
        expect(String(readPath)).toContain('.generated');
    });

    it('should return undefined when neither override nor bundled read succeed', async () => {
        const overrideWatcher = createFakeOverrideWatcher();
        vi.mocked(readFile).mockRejectedValue(new Error('missing'));

        const projector = new InstructionsFileContentProjector(
            '/ext', overrideWatcher, createFakeManager(), new InstructionsFileSectionsCache(), createFakeLogger(),
        );
        const result = await projector.project('does-not-exist.instructions.md');

        expect(result).toBeUndefined();
    });

    it('should reflect post-filter content for bundled bodies that omit a disabled bullet', async () => {
        // Simulates `.generated/` output where the manager already removed
        // the bullet associated with a disabled instruction.
        const filtered = `# C#

## Heading A

- **Do** use \`var\` for obvious types.
`;
        vi.mocked(readFile).mockResolvedValue(filtered);

        const projector = new InstructionsFileContentProjector(
            '/ext', createFakeOverrideWatcher(), createFakeManager(), new InstructionsFileSectionsCache(), createFakeLogger(),
        );
        const result = await projector.project('lang-csharp.instructions.md');

        expect(result?.body).toBe(filtered);
        expect(result?.body).not.toContain('async void');
        expect(result?.sections.map(s => s.heading)).toEqual(['Heading A']);
    });
});
