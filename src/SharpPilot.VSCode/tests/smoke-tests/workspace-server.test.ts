import * as assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { activatedExtension } from './helpers.js';

function poll(fn: () => boolean, interval: number, timeout: number): Promise<void> {
    return new Promise((resolve, reject) => {
        const start = Date.now();
        const check = () => {
            if (fn()) { resolve(); return; }
            if (Date.now() - start > timeout) { reject(new Error('Poll timed out')); return; }
            setTimeout(check, interval);
        };
        check();
    });
}

suite('Workspace Server Smoke Tests', () => {
    test('should have a pipe name when the server binary is available', async function () {
        const ext = await activatedExtension();
        const binary = join(ext.extensionPath, 'mcp', 'SharpPilot.WorkspaceServer', `SharpPilot.WorkspaceServer${process.platform === 'win32' ? '.exe' : ''}`);
        if (!existsSync(binary)) {
            this.skip();
            return;
        }

        await poll(() => !!ext.exports.workspaceServer.getPipeName(), 100, 8000);

        const pipeName = ext.exports.workspaceServer.getPipeName();
        assert.ok(pipeName, 'Workspace server pipe name should be available');
        assert.match(pipeName, /sharppilot-workspace-/, 'Pipe name should contain sharppilot-workspace- prefix');
    });
});
