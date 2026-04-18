import { describe, it, expect, vi, beforeEach } from 'vitest';
import { window } from './__mocks__/vscode';
import { AutoConfigurer } from '../../src/auto-configurer';
import { ContextKeys } from '../../src/context-keys';
import { InstructionsCatalog } from '../../src/instructions-catalog';
import { McpToolsCatalog } from '../../src/mcp-tools-catalog';
import { instructionsFiles, mcpTools } from '../../src/ui-constants';
import type { AutoContextConfig } from '../../src/autocontext-config';
import type { AutoContextConfigManager } from '../../src/autocontext-config';

const fakeDetector = {
    get: vi.fn((_key: string) => false),
} as unknown as import('../../src/workspace-context-detector').WorkspaceContextDetector;

let currentConfig: AutoContextConfig = {};
const fakeConfigManager = {
    read: vi.fn(async () => currentConfig),
    setInstructionEnabled: vi.fn(async () => {}),
    setMcpTools: vi.fn(async () => {}),
} as unknown as AutoContextConfigManager;

beforeEach(() => {
    vi.clearAllMocks();
    currentConfig = {};
    vi.mocked(fakeConfigManager.read).mockImplementation(async () => currentConfig);
    vi.mocked(fakeDetector.get).mockReset();
});

describe('AutoConfigurer', () => {
    const instructionsCatalog = new InstructionsCatalog(instructionsFiles);
    const catalog = new McpToolsCatalog(mcpTools);

    it('should disable context-dependent entries when nothing is detected', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        await new AutoConfigurer(fakeDetector, instructionsCatalog, catalog, fakeConfigManager).run();

        const setInstructionEnabled = vi.mocked(fakeConfigManager.setInstructionEnabled);
        const calls = setInstructionEnabled.mock.calls;

        // Only context-dependent entries should be disabled (always-on entries get enabled=true, not changed from default)
        expect.soft(calls.length).toBeGreaterThan(0);
        // Every disabled call should pass false
        const disabledCalls = calls.filter(([, value]) => value === false);
        expect.soft(disabledCalls.length).toBeGreaterThan(0);
        // All disabled calls must be for entries that have contextKeys
        for (const [fileName] of disabledCalls) {
            const entry = instructionsCatalog.findByFileName(fileName)!;
            expect.soft(ContextKeys.forEntry(entry).length).toBeGreaterThan(0);
        }
    });

    it('should not disable .NET entries when hasDotNet and hasCSharp are detected', async () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasDotNet' || key === 'hasCSharp');

        await new AutoConfigurer(fakeDetector, instructionsCatalog, catalog, fakeConfigManager).run();

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
        // check_csharp_all is a parent tool — should NOT be set to false
        expect.soft(newTools['check_csharp_all']).not.toBe(false);
    });

    it('should show an info message with the count of enabled items', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        await new AutoConfigurer(fakeDetector, instructionsCatalog, catalog, fakeConfigManager).run();

        const allEntries = [...instructionsCatalog.all, ...catalog.all];
        const alwaysOnCount = allEntries.filter(e => ContextKeys.forEntry(e).length === 0).length;

        expect.soft(window.showInformationMessage).toHaveBeenCalledWith(
            `AutoContext: Enabled ${alwaysOnCount} of ${allEntries.length} items for this workspace.`,
        );
    });

    it('should not call setInstructionEnabled for entries that already match the target state', async () => {
        // Pre-set dotnet-async-await as already disabled
        currentConfig = { instructions: { 'dotnet-async-await.instructions.md': { enabled: false } } };
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        await new AutoConfigurer(fakeDetector, instructionsCatalog, catalog, fakeConfigManager).run();

        const setInstructionEnabled = vi.mocked(fakeConfigManager.setInstructionEnabled);
        const updatedFileNames = setInstructionEnabled.mock.calls.map(([fileName]) => fileName);

        // It was already disabled, so setInstructionEnabled should NOT be called for this entry
        expect.soft(updatedFileNames).not.toContain('dotnet-async-await.instructions.md');
    });
});
