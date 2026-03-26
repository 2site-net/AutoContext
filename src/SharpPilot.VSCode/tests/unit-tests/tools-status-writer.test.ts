import { describe, it, expect, vi, beforeEach } from 'vitest';
import { __setConfigStore } from './__mocks__/vscode';
import { workspace } from './__mocks__/vscode';

import { readFileSync, writeFileSync, unlinkSync } from 'node:fs';

vi.mock('node:fs', () => ({
    readFileSync: vi.fn(),
    writeFileSync: vi.fn(),
    existsSync: vi.fn(),
    readdirSync: vi.fn(),
    rmSync: vi.fn(),
    statSync: vi.fn(),
    unlinkSync: vi.fn(),
}));

// Must import after the mock is set up via the vitest alias
import { ToolsStatusWriter } from '../../src/tools-status-writer';
import { SharpPilotConfigManager } from '../../src/sharppilot-config';

beforeEach(() => {
    vi.clearAllMocks();
    __setConfigStore({});
    workspace.workspaceFolders = [{ uri: { fsPath: '/workspace' } }];
});

describe('ToolsStatusWriter', () => {
    it('should write disabled tools to .sharppilot.json', () => {
        __setConfigStore({
            'sharppilot.tools.check_csharp_coding_style': false,
        });

        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) return '{}';
            return '';
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new ToolsStatusWriter(configManager);
        writer.write();

        const writeCalls = vi.mocked(writeFileSync).mock.calls;
        expect(writeCalls).toHaveLength(1);

        const [filePath, content] = writeCalls[0];
        expect(filePath).toMatch(/\.sharppilot\.json$/);

        const parsed = JSON.parse(content as string);
        expect(parsed.tools.disabledTools).toEqual(['check_csharp_coding_style']);
    });

    it('should not write when nothing changed', () => {
        __setConfigStore({
            'sharppilot.tools.check_csharp_coding_style': false,
        });

        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) {
                return JSON.stringify({ tools: { disabledTools: ['check_csharp_coding_style'] } });
            }
            return '';
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new ToolsStatusWriter(configManager);
        writer.write();

        expect(writeFileSync).not.toHaveBeenCalled();
    });

    it('should delete config file when all tools are enabled and no other config exists', () => {
        __setConfigStore({});

        vi.mocked(readFileSync).mockImplementation((path: unknown) => {
            const pathStr = String(path);
            if (pathStr.endsWith('.sharppilot.json')) {
                return JSON.stringify({ tools: { disabledTools: ['check_csharp_coding_style'] } });
            }
            return '';
        });

        const configManager = new SharpPilotConfigManager('/ext', '0.5.0');
        const writer = new ToolsStatusWriter(configManager);
        writer.write();

        // Config is empty — file should be deleted, not written.
        expect(writeFileSync).not.toHaveBeenCalled();
        expect(unlinkSync).toHaveBeenCalled();
    });
});
