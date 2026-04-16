import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { __emitterInstances } from './__mocks__/vscode';
import { connect, type Socket } from 'node:net';
import { HealthMonitorServer } from '../../src/health-monitor';

const fakeOutputChannel = {
    appendLine: vi.fn(),
    dispose: vi.fn(),
} as unknown as import('vscode').OutputChannel;

function pipePath(pipeName: string): string {
    return process.platform === 'win32'
        ? `\\\\.\\pipe\\${pipeName}`
        : `/tmp/CoreFxPipe_${pipeName}`;
}

function connectAndSend(pipe: string, category: string): Promise<Socket> {
    return new Promise((resolve, reject) => {
        const socket = connect(pipePath(pipe), () => {
            socket.write(category, () => resolve(socket));
        });
        socket.on('error', reject);
    });
}

/** Poll a condition until it becomes true (or timeout). */
async function waitFor(condition: () => boolean, ms = 2000, interval = 10): Promise<void> {
    const start = Date.now();
    while (!condition()) {
        if (Date.now() - start > ms) { throw new Error('waitFor timeout'); }
        await new Promise(r => setTimeout(r, interval));
    }
}

describe('HealthMonitorServer', () => {
    let monitor: HealthMonitorServer;
    let emittersBefore: number;

    beforeEach(() => {
        vi.clearAllMocks();
        emittersBefore = __emitterInstances.length;
        monitor = new HealthMonitorServer(fakeOutputChannel);
        monitor.start();
    });

    afterEach(() => {
        monitor.dispose();
    });

    /** Get the EventEmitter instance created by this monitor. */
    function getEmitter() { return __emitterInstances[emittersBefore]; }

    it('should report not running before any connection', () => {
        expect(monitor.isRunning('dotnet')).toBe(false);
    });

    it('should report running after a client connects', async () => {
        const socket = await connectAndSend(monitor.getPipeName(), 'dotnet');

        await waitFor(() => monitor.isRunning('dotnet'));
        expect(monitor.isRunning('dotnet')).toBe(true);

        socket.destroy();
    });

    it('should report not running after a client disconnects', async () => {
        const socket = await connectAndSend(monitor.getPipeName(), 'dotnet');
        await waitFor(() => monitor.isRunning('dotnet'));

        socket.destroy();
        await waitFor(() => !monitor.isRunning('dotnet'));

        expect(monitor.isRunning('dotnet')).toBe(false);
    });

    it('should track multiple categories independently', async () => {
        const s1 = await connectAndSend(monitor.getPipeName(), 'dotnet');
        await waitFor(() => monitor.isRunning('dotnet'));

        const s2 = await connectAndSend(monitor.getPipeName(), 'typescript');
        await waitFor(() => monitor.isRunning('typescript'));

        expect(monitor.isRunning('dotnet')).toBe(true);
        expect(monitor.isRunning('typescript')).toBe(true);

        s1.destroy();
        await waitFor(() => !monitor.isRunning('dotnet'));

        expect(monitor.isRunning('dotnet')).toBe(false);
        expect(monitor.isRunning('typescript')).toBe(true);

        s2.destroy();
    });

    it('should fire onDidChange on connect and disconnect', async () => {
        const emitter = getEmitter();
        expect(emitter.fire).not.toHaveBeenCalled();

        const socket = await connectAndSend(monitor.getPipeName(), 'git');
        await waitFor(() => monitor.isRunning('git'));

        expect(emitter.fire).toHaveBeenCalled();
        const callsAfterConnect = emitter.fire.mock.calls.length;

        socket.destroy();
        await waitFor(() => !monitor.isRunning('git'));

        expect(emitter.fire.mock.calls.length).toBeGreaterThan(callsAfterConnect);
    });

    describe('group health', () => {
        it('should report group healthy when all categories are running', async () => {
            const s1 = await connectAndSend(monitor.getPipeName(), 'git');
            await waitFor(() => monitor.isRunning('git'));

            const s2 = await connectAndSend(monitor.getPipeName(), 'editorconfig');
            await waitFor(() => monitor.isRunning('editorconfig'));

            expect(monitor.isGroupHealthy('Workspace')).toBe(true);
            expect(monitor.isGroupPartiallyHealthy('Workspace')).toBe(true);

            s1.destroy();
            s2.destroy();
        });

        it('should report group partially healthy when only some categories are running', async () => {
            const socket = await connectAndSend(monitor.getPipeName(), 'git');
            await waitFor(() => monitor.isRunning('git'));

            expect(monitor.isGroupHealthy('Workspace')).toBe(false);
            expect(monitor.isGroupPartiallyHealthy('Workspace')).toBe(true);

            socket.destroy();
        });

        it('should report group not healthy when no categories are running', () => {
            expect(monitor.isGroupHealthy('Workspace')).toBe(false);
            expect(monitor.isGroupPartiallyHealthy('Workspace')).toBe(false);
        });

        it('should report single-category group as healthy when running', async () => {
            const socket = await connectAndSend(monitor.getPipeName(), 'dotnet');
            await waitFor(() => monitor.isRunning('dotnet'));

            expect(monitor.isGroupHealthy('.NET')).toBe(true);

            socket.destroy();
        });

        it('should return false for unknown group', () => {
            expect(monitor.isGroupHealthy('Unknown')).toBe(false);
            expect(monitor.isGroupPartiallyHealthy('Unknown')).toBe(false);
        });
    });

    it('should return a pipe name', () => {
        expect(monitor.getPipeName()).toMatch(/^autocontext-health-[a-f0-9]{12}$/);
    });
});
