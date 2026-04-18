import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { __emitterInstances } from './_fakes/fake-vscode';
import { HealthMonitorServer } from '../../src/health-monitor';
import { createFakeOutputChannel } from './_fakes';
import { pipePath, connectAndSend } from './_utils/pipe-helpers';
import { waitFor } from './_utils/wait-for';

const fakeOutputChannel = createFakeOutputChannel();

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

    describe('server health', () => {
        it('should report server healthy when all scopes are running', async () => {
            const s1 = await connectAndSend(monitor.getPipeName(), 'git');
            await waitFor(() => monitor.isRunning('git'));

            const s2 = await connectAndSend(monitor.getPipeName(), 'editorconfig');
            await waitFor(() => monitor.isRunning('editorconfig'));

            expect(monitor.isServerHealthy('Workspace')).toBe(true);
            expect(monitor.isServerPartiallyHealthy('Workspace')).toBe(true);

            s1.destroy();
            s2.destroy();
        });

        it('should report server partially healthy when only some scopes are running', async () => {
            const socket = await connectAndSend(monitor.getPipeName(), 'git');
            await waitFor(() => monitor.isRunning('git'));

            expect(monitor.isServerHealthy('Workspace')).toBe(false);
            expect(monitor.isServerPartiallyHealthy('Workspace')).toBe(true);

            socket.destroy();
        });

        it('should report server not healthy when no scopes are running', () => {
            expect(monitor.isServerHealthy('Workspace')).toBe(false);
            expect(monitor.isServerPartiallyHealthy('Workspace')).toBe(false);
        });

        it('should report single-scope server as healthy when running', async () => {
            const socket = await connectAndSend(monitor.getPipeName(), 'dotnet');
            await waitFor(() => monitor.isRunning('dotnet'));

            expect(monitor.isServerHealthy('.NET')).toBe(true);

            socket.destroy();
        });

        it('should return false for unknown server label', () => {
            expect(monitor.isServerHealthy('Unknown')).toBe(false);
            expect(monitor.isServerPartiallyHealthy('Unknown')).toBe(false);
        });
    });

    it('should return a pipe name', () => {
        expect(monitor.getPipeName()).toMatch(/^autocontext-health-[a-f0-9]{12}$/);
    });
});
