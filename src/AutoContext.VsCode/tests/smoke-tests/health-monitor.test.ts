import * as assert from 'node:assert/strict';
import { connect, type Socket } from 'node:net';
import { activatedExtension } from './helpers.js';

function pipePath(name: string): string {
    return process.platform === 'win32'
        ? `\\\\.\\pipe\\${name}`
        : `/tmp/CoreFxPipe_${name}`;
}

function connectToPipe(name: string, timeoutMs: number): Promise<Socket> {
    return new Promise((resolve, reject) => {
        const socket = connect(pipePath(name));
        const timer = setTimeout(() => {
            socket.destroy();
            reject(new Error(`Timed out connecting to pipe '${name}' after ${timeoutMs}ms`));
        }, timeoutMs);
        socket.once('connect', () => {
            clearTimeout(timer);
            resolve(socket);
        });
        socket.once('error', (err) => {
            clearTimeout(timer);
            reject(err);
        });
    });
}

async function waitFor(predicate: () => boolean, timeoutMs: number, label: string): Promise<void> {
    const start = Date.now();
    while (!predicate()) {
        if (Date.now() - start > timeoutMs) {
            throw new Error(`Timed out waiting for ${label} after ${timeoutMs}ms`);
        }
        await new Promise(r => setTimeout(r, 25));
    }
}

suite('Health Monitor Smoke Tests', () => {
    test('should expose a stable pipe name', async () => {
        const { exports } = await activatedExtension();
        const name = exports.healthMonitor.getPipeName();

        assert.match(name, /^autocontext-health-/, `Expected an autocontext-health-* pipe name, got '${name}'`);
    });

    test('should accept a pipe connection and report the worker as running', async () => {
        const { exports } = await activatedExtension();
        const monitor = exports.healthMonitor;
        const pipeName = monitor.getPipeName();

        assert.ok(!monitor.isRunning('smoke-fake'), 'Sanity: no fake worker connected yet');

        const socket = await connectToPipe(pipeName, 2000);
        try {
            socket.write('smoke-fake');
            await waitFor(() => monitor.isRunning('smoke-fake'), 2000, 'isRunning(smoke-fake)=true');
        }
        finally {
            socket.destroy();
        }

        await waitFor(() => !monitor.isRunning('smoke-fake'), 2000, 'isRunning(smoke-fake)=false after disconnect');
    });
});
