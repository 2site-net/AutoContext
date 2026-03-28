import { describe, it, expect, vi, beforeEach } from 'vitest';
import { workspace, window, __setConfigStore } from './__mocks__/vscode';
import { AutoConfigurer } from '../../src/auto-configurer';
import { ContextKeys } from '../../src/context-keys';
import { instructionsCatalog } from '../../src/instructions-catalog';
import { toolsCatalog } from '../../src/tools-catalog';

const fakeDetector = {
    get: vi.fn((_key: string) => false),
} as unknown as import('../../src/workspace-context-detector').WorkspaceContextDetector;

beforeEach(() => {
    vi.clearAllMocks();
    __setConfigStore({});
    vi.mocked(fakeDetector.get).mockReset();
});

describe('AutoConfigurer.configure', () => {
    it('should enable always-on entries and disable others when nothing is detected', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        await AutoConfigurer.configure(fakeDetector);

        const config = vi.mocked(workspace.getConfiguration).mock.results[0].value;
        const updates = vi.mocked(config.update).mock.calls;

        // Always-on entries (copilot, codeReview, etc.) default to true and should stay true → no update needed.
        // Workspace-specific entries default to true but workspace says false → should be disabled.
        expect(updates.length).toBeGreaterThan(0);

        for (const [settingId, value] of updates) {
            expect(value).toBe(false);
            expect(settingId).toMatch(/^sharppilot\./);
        }
    });

    it('should enable .NET entries when hasDotnet is detected', async () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasDotnet');

        await AutoConfigurer.configure(fakeDetector);

        const config = vi.mocked(workspace.getConfiguration).mock.results[0].value;
        const updates = vi.mocked(config.update).mock.calls;

        // .NET entries should NOT appear in updates (already true by default, and relevant).
        // Non-.NET workspace entries should be disabled.
        const updatedIds = new Set(updates.map(([id]: [string]) => id));

        expect(updatedIds.has('sharppilot.instructions.dotnet.asyncAwait')).toBe(false);
        expect(updatedIds.has('sharppilot.tools.check_csharp_coding_style')).toBe(false);

        // Git entries should be disabled
        expect(updatedIds.has('sharppilot.instructions.git.commitFormat')).toBe(true);
    });

    it('should show an info message with the count of enabled items', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        await AutoConfigurer.configure(fakeDetector);

        const allEntries = [...instructionsCatalog.all, ...toolsCatalog.all];
        const alwaysOnCount = allEntries.filter(e => ContextKeys.forEntry(e).length === 0).length;

        expect(window.showInformationMessage).toHaveBeenCalledWith(
            `SharpPilot: Enabled ${alwaysOnCount} of ${allEntries.length} items for this workspace.`,
        );
    });

    it('should not update settings that already match the target state', async () => {
        __setConfigStore({
            'sharppilot.instructions.dotnet.asyncAwait': false,
        });
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        await AutoConfigurer.configure(fakeDetector);

        const config = vi.mocked(workspace.getConfiguration).mock.results[0].value;
        const updatedIds = vi.mocked(config.update).mock.calls.map(([id]: [string]) => id);

        // Already false, should not be updated again
        expect(updatedIds.filter((id: string) => id === 'sharppilot.instructions.dotnet.asyncAwait')).toHaveLength(0);
    });
});
