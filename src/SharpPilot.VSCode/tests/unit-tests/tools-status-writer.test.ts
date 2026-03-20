import { describe, it, expect, vi, beforeEach } from 'vitest';
import { __setConfigStore } from './__mocks__/vscode';

// Must import after the mock is set up via the vitest alias
import { ToolsStatusWriter } from '../../src/tools-status-writer';

import { writeFileSync, mkdirSync } from 'node:fs';

vi.mock('node:fs', () => ({
    writeFileSync: vi.fn(),
    mkdirSync: vi.fn(),
}));

beforeEach(() => {
    vi.clearAllMocks();
    __setConfigStore({});
});

describe('ToolsStatusWriter', () => {
    it('should write a JSON file with tool statuses', () => {
        __setConfigStore({
            'sharp-pilot.tools.check_csharp_coding_style': false,
        });

        const writer = new ToolsStatusWriter('/servers');
        writer.write();

        expect(mkdirSync).toHaveBeenCalledWith(
            expect.stringContaining('SharpPilot'),
            { recursive: true },
        );

        const writeCalls = vi.mocked(writeFileSync).mock.calls;

        expect(writeCalls).toHaveLength(1);

        const [filePath, content] = writeCalls[0];

        expect(filePath).toMatch(/tools-status\.json$/);

        const parsed = JSON.parse(content as string);

        expect(parsed.check_csharp_coding_style).toBe(false);
        // Tools not in configStore should default to true
        expect(parsed.check_csharp_async_patterns).toBe(true);
    });

    it('should default all tools to true when no config is set', () => {
        const writer = new ToolsStatusWriter('/servers');
        writer.write();

        const content = vi.mocked(writeFileSync).mock.calls[0][1] as string;
        const parsed = JSON.parse(content);

        for (const value of Object.values(parsed)) {
            expect(value).toBe(true);
        }
    });
});
