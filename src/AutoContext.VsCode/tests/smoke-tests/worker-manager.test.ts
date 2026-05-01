import * as assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { activatedExtension } from './helpers.js';

function withTimeout<T>(promise: Promise<T>, ms: number, label: string): Promise<T> {
    let timer: NodeJS.Timeout;
    const timeout = new Promise<T>((_, reject) => {
        timer = setTimeout(() => reject(new Error(`${label} timed out after ${ms}ms`)), ms);
    });
    return Promise.race([promise, timeout]).finally(() => clearTimeout(timer));
}

suite('Worker Manager Smoke Tests', () => {
    test('should generate a per-window instance id', async () => {
        const ext = await activatedExtension();
        const instanceId = ext.exports.workerManager.getInstanceId();

        assert.match(instanceId, /^[0-9a-f]{12}$/, 'Instance id should be 12 hex characters');
    });

    test('should reach Worker.Workspace ready when the binary is available', async function () {
        const ext = await activatedExtension();
        const binary = join(ext.extensionPath, 'servers', 'AutoContext.Worker.Workspace', `AutoContext.Worker.Workspace${process.platform === 'win32' ? '.exe' : ''}`);
        if (!existsSync(binary)) {
            this.skip();
            return;
        }

        await withTimeout(ext.exports.workerManager.whenWorkspaceReady(), 8000, 'Worker.Workspace ready marker');
    });
});
