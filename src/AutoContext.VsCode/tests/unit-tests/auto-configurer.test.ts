import { describe, it, expect, vi, beforeEach } from 'vitest';
import { join } from 'node:path';
import { window } from '#testing/fakes/fake-vscode';
import { AutoConfigurer } from '../../src/auto-configurer';
import { InstructionsFilesManifestLoader } from '../../src/instructions-files-manifest-loader';
import { McpToolsManifestLoader } from '../../src/mcp-tools-manifest-loader';
import type { AutoContextConfig } from '#types/autocontext-config.js';
import { createFakeDetector, createFakeConfigManager } from '#testing/fakes';

const fakeDetector = createFakeDetector();

let currentConfig: AutoContextConfig = {};
const fakeConfigManager = createFakeConfigManager();

beforeEach(() => {
    vi.clearAllMocks();
    currentConfig = {};
    vi.mocked(fakeConfigManager.read).mockImplementation(async () => currentConfig);
    vi.mocked(fakeDetector.get).mockReset();
});

describe('AutoConfigurer', () => {
    const extensionPath = join(__dirname, '..', '..');
    const instructionsManifest = new InstructionsFilesManifestLoader(extensionPath).load();
    const manifest = new McpToolsManifestLoader(extensionPath).load();

    it('should disable context-dependent entries when nothing is detected', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        await new AutoConfigurer(fakeDetector, instructionsManifest, manifest, fakeConfigManager).run();

        const setInstructionEnabled = vi.mocked(fakeConfigManager.setInstructionEnabled);
        const calls = setInstructionEnabled.mock.calls;

        // Only context-dependent entries should be disabled (always-on entries get enabled=true, not changed from default)
        expect.soft(calls.length).toBeGreaterThan(0);
        // Every disabled call should pass false
        const disabledCalls = calls.filter(([, value]) => value === false);
        expect.soft(disabledCalls.length).toBeGreaterThan(0);
        // All disabled calls must be for entries that have activationFlags
        const disabledEntryFlagCounts = disabledCalls.map(
            ([fileName]) => instructionsManifest.findByName(fileName)!.activationFlags.length,
        );
        expect.soft(disabledEntryFlagCounts.every((n) => n > 0)).toBe(true);
    });

    it('should not disable .NET entries when hasDotNet and hasCSharp are detected', async () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasDotNet' || key === 'hasCSharp');

        await new AutoConfigurer(fakeDetector, instructionsManifest, manifest, fakeConfigManager).run();

        const setInstructionEnabled = vi.mocked(fakeConfigManager.setInstructionEnabled);
        const disabledFileNames = setInstructionEnabled.mock.calls
            .filter(([, value]) => value === false)
            .map(([fileName]) => fileName);

        // .NET async-await should NOT be disabled since hasDotNet is detected
        expect.soft(disabledFileNames).not.toContain('dotnet-async-await.instructions.md');
        // git commit format requires hasGit which is not detected, so it WILL be disabled
        expect.soft(disabledFileNames).toContain('git-commit-format.instructions.md');

        // MCP tools: check that C# tools are NOT in the disabled list
        const setMcpTools = vi.mocked(fakeConfigManager.setMcpTools);
        expect.soft(setMcpTools).toHaveBeenCalledOnce();
        const [newTools] = setMcpTools.mock.calls[0];
        // analyze_csharp_code is a parent tool — should NOT be set to false
        expect.soft(newTools['analyze_csharp_code']).not.toBe(false);
    });

    it('should show an info message with the count of enabled items', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        await new AutoConfigurer(fakeDetector, instructionsManifest, manifest, fakeConfigManager).run();

        const allInstructions = instructionsManifest.instructions;
        const totalToolItems = manifest.tools.reduce((acc, t) => acc + Math.max(1, t.tasks.length), 0);
        const totalItems = allInstructions.length + totalToolItems;
        const instructionAlwaysOn = allInstructions.filter(e => e.activationFlags.length === 0).length;
        const toolAlwaysOn = manifest.tools
            .filter(t => t.activationFlags.length === 0)
            .reduce((acc, t) => acc + Math.max(1, t.tasks.length), 0);
        const alwaysOnCount = instructionAlwaysOn + toolAlwaysOn;

        expect.soft(window.showInformationMessage).toHaveBeenCalledWith(
            `AutoContext: Enabled ${alwaysOnCount} of ${totalItems} items for this workspace.`,
        );
    });

    it('should not call setInstructionEnabled for entries that already match the target state', async () => {
        // Pre-set dotnet-async-await as already disabled
        currentConfig = { instructions: { 'dotnet-async-await.instructions.md': { enabled: false } } };
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        await new AutoConfigurer(fakeDetector, instructionsManifest, manifest, fakeConfigManager).run();

        const setInstructionEnabled = vi.mocked(fakeConfigManager.setInstructionEnabled);
        const updatedFileNames = setInstructionEnabled.mock.calls.map(([fileName]) => fileName);

        // It was already disabled, so setInstructionEnabled should NOT be called for this entry
        expect.soft(updatedFileNames).not.toContain('dotnet-async-await.instructions.md');
    });
});
