import * as assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { connect, type Socket } from 'node:net';
import { join } from 'node:path';
import { activatedExtension } from './helpers.js';

const exe = (name: string) =>
    process.platform === 'win32' ? `${name}.exe` : name;

function workerBinary(extensionPath: string, name: string): string {
    return join(extensionPath, 'servers', name, exe(name));
}

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

    test('should report Worker.Workspace as running once it has reached its ready marker', async function () {
        const ext = await activatedExtension();
        if (!existsSync(workerBinary(ext.extensionPath, 'AutoContext.Worker.Workspace'))) {
            this.skip();
            return;
        }

        await ext.exports.workerManager.whenWorkspaceReady();

        // The worker writes its id to the health pipe right after host
        // startup; allow a short grace period for the server-side accept
        // callback to register the connection.
        await waitFor(() => ext.exports.healthMonitor.isRunning('workspace'), 2000, 'isRunning(workspace)=true');
    });

    test('should report Worker.DotNet as running once explicitly spawned', async function () {
        const ext = await activatedExtension();
        if (!existsSync(workerBinary(ext.extensionPath, 'AutoContext.Worker.DotNet'))) {
            this.skip();
            return;
        }

        // Workers spawn lazily — trigger one explicitly and then check
        // that the health pipe sees it.
        await ext.exports.workerManager.ensureRunning('Worker.DotNet');

        await waitFor(() => ext.exports.healthMonitor.isRunning('dotnet'), 8000, 'isRunning(dotnet)=true');
    });

    test('should report Worker.Web as running once explicitly spawned', async function () {
        const ext = await activatedExtension();
        const bundle = join(ext.extensionPath, 'servers', 'AutoContext.Worker.Web', 'index.js');
        if (!existsSync(bundle)) {
            this.skip();
            return;
        }

        await ext.exports.workerManager.ensureRunning('Worker.Web');

        await waitFor(() => ext.exports.healthMonitor.isRunning('web'), 8000, 'isRunning(web)=true');
    });

    test('should report mcp-server as running while the spawned process is alive', async function () {
        const ext = await activatedExtension();
        const mcpBinary = join(ext.extensionPath, 'servers', 'AutoContext.Mcp.Server', exe('AutoContext.Mcp.Server'));
        if (!existsSync(mcpBinary)) {
            this.skip();
            return;
        }

        const monitor = ext.exports.healthMonitor;
        assert.ok(!monitor.isRunning('mcp-server'), 'Sanity: mcp-server should not already be running');

        const defs = await ext.exports.mcpServerProvider.provideMcpServerDefinitions();
        assert.ok(defs.length > 0, 'No MCP server definitions returned');
        const def = defs[0];

        // Spawn the real Mcp.Server binary directly so we control the
        // process lifetime (VS Code only spawns it lazily when a tool is
        // invoked, which doesn't happen during smoke runs).
        const child = spawn(def.command, def.args, { stdio: ['pipe', 'pipe', 'pipe'] });
        try {
            await waitFor(() => monitor.isRunning('mcp-server'), 8000, 'isRunning(mcp-server)=true');
        }
        finally {
            child.kill();
            await new Promise<void>((resolve) => child.once('exit', () => resolve()));
        }

        await waitFor(() => !monitor.isRunning('mcp-server'), 4000, 'isRunning(mcp-server)=false after exit');
    });

    test('should not register any worker when a client connects but never sends an id', async () => {
        const { exports } = await activatedExtension();
        const monitor = exports.healthMonitor;
        const pipeName = monitor.getPipeName();

        const socket = await connectToPipe(pipeName, 2000);
        try {
            // Give the server a beat to (mis)handle the silent connection.
            await new Promise(r => setTimeout(r, 200));

            assert.ok(!monitor.isRunning(''), 'Server must not register an empty-string worker id');
        }
        finally {
            socket.destroy();
        }
    });

    test('should ignore an empty/whitespace id and not register the connection', async () => {
        const { exports } = await activatedExtension();
        const monitor = exports.healthMonitor;
        const pipeName = monitor.getPipeName();

        const socket = await connectToPipe(pipeName, 2000);
        try {
            socket.write('   \r\n  ');
            await new Promise(r => setTimeout(r, 200));

            assert.ok(!monitor.isRunning(''), 'Whitespace id must not register an empty-string entry');
        }
        finally {
            socket.destroy();
        }
    });

    test('should treat arbitrary bytes as an opaque id without crashing or corrupting state', async () => {
        const { exports } = await activatedExtension();
        const monitor = exports.healthMonitor;
        const pipeName = monitor.getPipeName();

        const garbage = Buffer.from([0x00, 0x01, 0xff, 0xfe, 0x7f, 0x80, 0x41, 0x42]);
        const garbageSocket = await connectToPipe(pipeName, 2000);
        try {
            garbageSocket.write(garbage);
            await new Promise(r => setTimeout(r, 200));

            // We don't care whether the garbage string registered under
            // its own opaque key — what matters is the server didn't
            // crash and didn't corrupt the empty-id slot.
            assert.ok(!monitor.isRunning(''), 'Garbage bytes must not register an empty-string entry');
        }
        finally {
            garbageSocket.destroy();
        }

        // Roundtrip a well-formed client immediately after the garbage
        // disconnect to prove the server is still healthy.
        const okSocket = await connectToPipe(pipeName, 2000);
        try {
            okSocket.write('smoke-after-garbage');
            await waitFor(() => monitor.isRunning('smoke-after-garbage'), 2000, 'isRunning(smoke-after-garbage)=true');
        }
        finally {
            okSocket.destroy();
        }
        await waitFor(() => !monitor.isRunning('smoke-after-garbage'), 2000, 'isRunning(smoke-after-garbage)=false after disconnect');
    });
});
