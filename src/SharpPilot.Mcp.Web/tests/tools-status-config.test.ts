import { describe, it, expect } from 'vitest';
import { writeFileSync, mkdirSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { configure, isEnabled } from '../src/tools-status-config.js';

describe('ToolsStatusConfig', () => {
    const testDir = join(tmpdir(), `sharppilot-test-${Date.now()}`);

    function writeConfig(content: string): void {
        mkdirSync(testDir, { recursive: true });
        writeFileSync(join(testDir, '.sharppilot.json'), content, 'utf-8');
    }

    function cleanup(): void {
        rmSync(testDir, { recursive: true, force: true });
    }

    it('should return true when no config file exists', () => {
        configure(join(tmpdir(), 'nonexistent-dir'));

        expect(isEnabled('check_typescript_coding_style')).toBe(true);
    });

    it('should return true when tool is not in disabled list', () => {
        writeConfig(JSON.stringify({
            tools: { disabledTools: ['some_other_tool'] },
        }));
        configure(testDir);

        expect(isEnabled('check_typescript_coding_style')).toBe(true);

        cleanup();
    });

    it('should return false when tool is in disabled list', () => {
        writeConfig(JSON.stringify({
            tools: { disabledTools: ['check_typescript_coding_style'] },
        }));
        configure(testDir);

        expect(isEnabled('check_typescript_coding_style')).toBe(false);

        cleanup();
    });

    it('should return true when config file is malformed', () => {
        writeConfig('not valid json');
        configure(testDir);

        expect(isEnabled('check_typescript_coding_style')).toBe(true);

        cleanup();
    });
});
