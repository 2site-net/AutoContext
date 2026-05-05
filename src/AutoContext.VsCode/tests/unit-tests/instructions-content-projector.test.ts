import { describe, it, expect, vi, beforeEach } from 'vitest';
import { readFile } from 'node:fs/promises';

vi.mock('node:fs/promises', () => ({
    readFile: vi.fn(),
}));

import { workspace } from '#testing/fakes/fake-vscode';
import { createFakeDetector, createFakeLogger } from '#testing/fakes';
import { InstructionsContentProjector } from '#src/instructions-content-projector';

const bundledBody = `---
name: "lang-csharp (v1.0.0)"
description: "C# rules"
applyTo: "**/*.cs"
---
# C#

- [INST0001] **Do** use \`var\` for obvious types.
- [INST0002] **Don't** use \`async void\`.
`;

const overrideBody = `---
name: "lang-csharp (v1.0.0)"
description: "Override"
---
# Overridden body

- **Do** override the bundled rule.
`;

describe('InstructionsContentProjector', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('reads the bundled file when no override is reported', async () => {
        vi.mocked(readFile).mockResolvedValue(bundledBody);
        const detector = createFakeDetector();
        const projector = new InstructionsContentProjector('/ext', detector, createFakeLogger());

        const result = await projector.project('lang-csharp.instructions.md');

        expect(detector.hasOverriddenFile).toHaveBeenCalledWith('lang-csharp.instructions.md');
        expect(workspace.findFiles).not.toHaveBeenCalled();
        expect(result).toBeDefined();
        expect(result).not.toContain('---');
        expect(result).not.toContain('description:');
        expect(result).not.toContain('[INST0001]');
        expect(result).not.toContain('[INST0002]');
        expect(result).toContain('# C#');
        expect(result).toContain('**Do** use');
        expect(result).toContain("**Don't** use");
    });

    it('reads the override file when the detector reports an override', async () => {
        const detector = createFakeDetector();
        vi.mocked(detector.hasOverriddenFile).mockReturnValue(true);

        const fakeUri = { path: '/ws/.github/instructions/lang-csharp.instructions.md' };
        vi.mocked(workspace.findFiles).mockResolvedValueOnce([fakeUri] as never);
        vi.mocked(workspace.fs.readFile).mockResolvedValueOnce(new TextEncoder().encode(overrideBody) as never);

        const projector = new InstructionsContentProjector('/ext', detector, createFakeLogger());
        const result = await projector.project('lang-csharp.instructions.md');

        expect(workspace.findFiles).toHaveBeenCalledWith('.github/instructions/lang-csharp.instructions.md', undefined, 1);
        expect(readFile).not.toHaveBeenCalled();
        expect(result).toContain('# Overridden body');
        expect(result).not.toContain('---');
        expect(result).not.toContain('description:');
    });

    it('falls back to the bundled file when the override read fails', async () => {
        const detector = createFakeDetector();
        vi.mocked(detector.hasOverriddenFile).mockReturnValue(true);
        vi.mocked(workspace.findFiles).mockResolvedValueOnce([] as never);
        vi.mocked(readFile).mockResolvedValue(bundledBody);

        const projector = new InstructionsContentProjector('/ext', detector, createFakeLogger());
        const result = await projector.project('lang-csharp.instructions.md');

        expect(result).toContain('# C#');
        expect(readFile).toHaveBeenCalled();
    });

    it('returns undefined when neither override nor bundled read succeed', async () => {
        const detector = createFakeDetector();
        vi.mocked(readFile).mockRejectedValue(new Error('missing'));

        const projector = new InstructionsContentProjector('/ext', detector, createFakeLogger());
        const result = await projector.project('does-not-exist.instructions.md');

        expect(result).toBeUndefined();
    });

    it('strips frontmatter that uses CRLF line endings', async () => {
        vi.mocked(readFile).mockResolvedValue(
            '---\r\ndescription: "x"\r\n---\r\n# Title\r\n',
        );
        const detector = createFakeDetector();
        const projector = new InstructionsContentProjector('/ext', detector, createFakeLogger());

        const result = await projector.project('x.instructions.md');

        expect(result).toBe('# Title\r\n');
    });
});
