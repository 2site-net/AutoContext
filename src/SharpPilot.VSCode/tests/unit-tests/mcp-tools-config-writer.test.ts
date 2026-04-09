import { describe, it, expect, vi, beforeEach } from 'vitest';
import { __setConfigStore } from './__mocks__/vscode';
import { workspace } from './__mocks__/vscode';

import { writeFile, unlink, readFile } from 'node:fs/promises';

vi.mock('node:fs/promises', () => ({
    writeFile: vi.fn().mockResolvedValue(undefined),
    unlink: vi.fn().mockResolvedValue(undefined),
    readFile: vi.fn().mockResolvedValue('{}'),
}));

// Must import after the mock is set up via the vitest alias
import { McpToolsConfigWriter } from '../../src/mcp-tools-config-writer';
import { McpToolsCatalog } from '../../src/mcp-tools-catalog';
import { mcpTools } from '../../src/ui-constants';
import { SharpPilotConfigManager } from '../../src/sharppilot-config';

beforeEach(() => {
    vi.clearAllMocks();
    __setConfigStore({});
    workspace.workspaceFolders = [{ uri: { fsPath: '/workspace' } }];
});

describe('McpToolsConfigWriter', () => {
    const catalog = new McpToolsCatalog(mcpTools);

    it('should write disabled tools to .sharppilot.json', async () => {
        __setConfigStore({
            'sharppilot.mcpTools.check_csharp_coding_style': false,
        });

        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return '';
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new McpToolsConfigWriter(configManager, catalog);
        await writer.write();

        const writeCalls = vi.mocked(writeFile).mock.calls;
        expect(writeCalls).toHaveLength(1);

        const [filePath, content] = writeCalls[0];
        expect(filePath).toMatch(/\.sharppilot\.json$/);

        const parsed = JSON.parse(content as string);
        expect.soft(parsed["mcp-tools"].disabled).toEqual(['check_csharp_coding_style']);
    });

    it('should not write when nothing changed', async () => {
        __setConfigStore({
            'sharppilot.mcpTools.check_csharp_coding_style': false,
        });

        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) {
                return JSON.stringify({ "mcp-tools": { disabled: ['check_csharp_coding_style'] } });
            }
            return '';
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new McpToolsConfigWriter(configManager, catalog);
        await writer.write();

        expect.soft(writeFile).not.toHaveBeenCalled();
    });

    it('should delete config file when all tools are enabled and no other config exists', async () => {
        __setConfigStore({});

        vi.mocked(readFile).mockImplementation(async (path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) {
                return JSON.stringify({ "mcp-tools": { disabled: ['check_csharp_coding_style'] } });
            }
            return '';
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new McpToolsConfigWriter(configManager, catalog);
        await writer.write();

        expect(writeFile).not.toHaveBeenCalled();
        expect.soft(unlink).toHaveBeenCalled();
    });
});
