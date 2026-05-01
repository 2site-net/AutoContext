import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { EventEmitter } from 'node:events';
import { PassThrough } from 'node:stream';
import { createFakeLogger } from '#testing/fakes';
import { ServerEntry } from '#src/server-entry';

const spawnMock = vi.fn();

vi.mock('node:child_process', () => ({
    spawn: (...args: unknown[]) => spawnMock(...args),
}));

// Import after mocks so the module picks up the mocked spawn.
const { WorkerManager } = await import('../../src/worker-manager');

const INSTANCE_ID = '0123456789ab';

const fakeWorkers: ServerEntry[] = [
    new ServerEntry('workspace', 'AutoContext.Worker.Workspace', 'dotnet'),
    new ServerEntry('dotnet', 'AutoContext.Worker.DotNet', 'dotnet'),
    new ServerEntry('web', 'AutoContext.Worker.Web', 'node'),
];

interface FakeChild extends EventEmitter {
    stdout: PassThrough;
    stderr: PassThrough;
    kill: ReturnType<typeof vi.fn>;
}

function createFakeChild(): FakeChild {
    const child = new EventEmitter() as FakeChild;
    child.stdout = new PassThrough();
    child.stderr = new PassThrough();
    child.kill = vi.fn();
    return child;
}

function ensureAll(mgr: InstanceType<typeof WorkerManager>): void {
    // Synchronously triggers spawn() for every registered worker.
    // Swallow rejections — spawn-time assertions read spawnMock.mock.calls,
    // and ready-marker assertions await the returned promise explicitly.
    for (const identity of ['Worker.Workspace', 'Worker.DotNet', 'Worker.Web']) {
        void mgr.ensureRunning(identity).catch(() => { /* tests that care await directly */ });
    }
}

describe('WorkerManager', () => {
    const logger = createFakeLogger();
    const infoSpy = logger.info as unknown as ReturnType<typeof vi.fn>;
    let manager: InstanceType<typeof WorkerManager>;

    beforeEach(() => {
        vi.clearAllMocks();
        spawnMock.mockImplementation(() => createFakeChild());
        manager = new WorkerManager('/ext', logger, '/workspace', fakeWorkers, INSTANCE_ID);
    });

    afterEach(() => {
        manager.dispose();
    });

    it('should expose the instance id', () => {
        expect(manager.getInstanceId()).toBe(INSTANCE_ID);
    });

    it('should accept a different instance id per construction', () => {
        const other = new WorkerManager('/ext', logger, '/workspace', fakeWorkers, 'cafebabe1234');

        expect(other.getInstanceId()).not.toBe(manager.getInstanceId());

        other.dispose();
    });

    it('should spawn each worker when ensureRunning is called', () => {
        ensureAll(manager);

        expect(spawnMock).toHaveBeenCalledTimes(3);
    });

    it('should pass --instance-id and a worker-role pipe address to every worker', () => {
        ensureAll(manager);

        const id = manager.getInstanceId();
        const commands = spawnMock.mock.calls.map(c => ({ cmd: c[0] as string, args: c[1] as string[] }));
        const workspaceCall = commands.find(c => c.cmd.includes('Worker.Workspace'));
        const dotnetCall = commands.find(c => c.cmd.includes('Worker.DotNet'));
        const webCall = commands.find(c => c.cmd === 'node');

        for (const call of [workspaceCall, dotnetCall, webCall]) {
            expect(call?.args).toContain('--instance-id');
            expect(call?.args).toContain(id);
        }

        // Workers self-format their listen address from instance-id +
        // worker-id; the extension no longer passes --pipe.
        for (const call of [workspaceCall, dotnetCall, webCall]) {
            expect(call?.args).not.toContain('--pipe');
        }
    });

    it('should pass --workspace-root to Worker.Workspace when provided', () => {
        void manager.ensureRunning('Worker.Workspace').catch(() => { /* readiness not asserted here */ });

        const workspaceCall = spawnMock.mock.calls.find(c => (c[0] as string).includes('Worker.Workspace'));

        expect(workspaceCall?.[1]).toContain('--workspace-root');
        expect(workspaceCall?.[1]).toContain('/workspace');
    });

    it('should omit --workspace-root when no workspace root is given', () => {
        const mgr = new WorkerManager('/ext', logger, undefined, fakeWorkers, INSTANCE_ID);
        void mgr.ensureRunning('Worker.Workspace').catch(() => { /* readiness not asserted here */ });

        const workspaceCall = spawnMock.mock.calls.find(c => (c[0] as string).includes('Worker.Workspace'));

        expect(workspaceCall?.[1]).not.toContain('--workspace-root');

        mgr.dispose();
    });

    it('should pass --service health-monitor=<address> to every worker when supplied', () => {
        const mgr = new WorkerManager('/ext', logger, '/workspace', fakeWorkers, INSTANCE_ID, undefined, 'autocontext.health-monitor#xyz');
        ensureAll(mgr);

        const calls = spawnMock.mock.calls.map(c => c[1] as string[]);

        expect(calls.length).toBe(3);
        for (const args of calls) {
            expect(args).toContain('--service');
            expect(args).toContain('health-monitor=autocontext.health-monitor#xyz');
        }

        mgr.dispose();
    });

    it('should omit --service health-monitor=... when no health-monitor address is supplied', () => {
        const mgr = new WorkerManager('/ext', logger, '/workspace', fakeWorkers, INSTANCE_ID);
        ensureAll(mgr);

        const calls = spawnMock.mock.calls.map(c => c[1] as string[]);

        for (const args of calls) {
            expect(args.some(a => a.startsWith('health-monitor='))).toBe(false);
        }

        mgr.dispose();
    });

    it('should forward stderr lines to the output channel with a process-identity prefix', async () => {
        const children: FakeChild[] = [];
        spawnMock.mockImplementation(() => {
            const child = createFakeChild();
            children.push(child);
            return child;
        });

        const mgr = new WorkerManager('/ext', logger, '/workspace', fakeWorkers, INSTANCE_ID);
        void mgr.ensureRunning('Worker.Workspace').catch(() => { /* readiness not asserted here */ });
        const workspaceChild = children[0];
        workspaceChild.stderr.write('hello from workspace\n');

        await new Promise(r => setImmediate(r));

        expect(logger.forCategory).toHaveBeenCalledWith('Worker.Workspace');
        expect(infoSpy).toHaveBeenCalledWith('hello from workspace');

        mgr.dispose();
    });

    it('should resolve whenWorkspaceReady when the workspace ready marker is observed', async () => {
        const children: FakeChild[] = [];
        spawnMock.mockImplementation(() => {
            const child = createFakeChild();
            children.push(child);
            return child;
        });

        const mgr = new WorkerManager('/ext', logger, '/workspace', fakeWorkers, INSTANCE_ID);
        let resolved = false;
        void mgr.whenWorkspaceReady().then(() => { resolved = true; });
        const workspaceChild = children[0];
        workspaceChild.stderr.write('[AutoContext.Worker.Workspace] Ready.\n');

        await new Promise(r => setImmediate(r));

        expect(resolved).toBe(true);

        mgr.dispose();
    });

    it('should kill every spawned child on dispose', () => {
        const children: FakeChild[] = [];
        spawnMock.mockImplementation(() => {
            const child = createFakeChild();
            children.push(child);
            return child;
        });

        const mgr = new WorkerManager('/ext', logger, '/workspace', fakeWorkers, INSTANCE_ID);
        ensureAll(mgr);
        mgr.dispose();

        expect(children).toHaveLength(3);
        expect(children.every(c => c.kill.mock.calls.length === 1)).toBe(true);
    });

    it('should reject pending whenWorkspaceReady() waiters on dispose', async () => {
        const mgr = new WorkerManager('/ext', logger, '/workspace', fakeWorkers, INSTANCE_ID);

        const ready = mgr.whenWorkspaceReady();
        mgr.dispose();

        await expect(ready).rejects.toThrow(/disposed before worker became ready/);
    });

    it('should reject whenWorkspaceReady() when the worker emits a spawn error', async () => {
        const children: FakeChild[] = [];
        spawnMock.mockImplementation(() => {
            const child = createFakeChild();
            children.push(child);
            return child;
        });

        const mgr = new WorkerManager('/ext', logger, '/workspace', fakeWorkers, INSTANCE_ID);

        const ready = mgr.whenWorkspaceReady();
        children[0].emit('error', new Error('spawn ENOENT'));

        await expect(ready).rejects.toThrow(/spawn ENOENT/);

        mgr.dispose();
    });

    it('should reject whenWorkspaceReady() when the worker exits before emitting ready', async () => {
        const children: FakeChild[] = [];
        spawnMock.mockImplementation(() => {
            const child = createFakeChild();
            children.push(child);
            return child;
        });

        const mgr = new WorkerManager('/ext', logger, '/workspace', fakeWorkers, INSTANCE_ID);

        const ready = mgr.whenWorkspaceReady();
        children[0].emit('exit', 1);

        await expect(ready).rejects.toThrow(/exited with code 1 before becoming ready/);

        mgr.dispose();
    });

    it('should not reject whenWorkspaceReady() when the worker exits cleanly after becoming ready', async () => {
        const children: FakeChild[] = [];
        spawnMock.mockImplementation(() => {
            const child = createFakeChild();
            children.push(child);
            return child;
        });

        const mgr = new WorkerManager('/ext', logger, '/workspace', fakeWorkers, INSTANCE_ID);

        const ready = mgr.whenWorkspaceReady();
        children[0].stderr.write('[AutoContext.Worker.Workspace] Ready.\n');
        await new Promise(r => setImmediate(r));

        // Once ready resolved, a later exit must not turn the resolved
        // promise into a rejection.
        children[0].emit('exit', 0);

        await expect(ready).resolves.toBeUndefined();

        mgr.dispose();
    });

    describe('ensureRunning', () => {
        it('should coalesce concurrent calls onto the same spawn', async () => {
            const mgr = new WorkerManager('/ext', logger, '/workspace', fakeWorkers, INSTANCE_ID);

            const a = mgr.ensureRunning('Worker.Workspace');
            const b = mgr.ensureRunning('Worker.Workspace');

            expect(spawnMock).toHaveBeenCalledTimes(1);
            expect(a).toBe(b);

            mgr.dispose();
            await expect(a).rejects.toThrow(/disposed/);
        });

        it('should reject with a clear error for an unknown worker identity', async () => {
            const mgr = new WorkerManager('/ext', logger, '/workspace', fakeWorkers, INSTANCE_ID);

            await expect(mgr.ensureRunning('Worker.Bogus')).rejects.toThrow(/No worker registered/);
            expect(spawnMock).not.toHaveBeenCalled();

            mgr.dispose();
        });

        it('should respawn after the previous child exits', async () => {
            const children: FakeChild[] = [];
            spawnMock.mockImplementation(() => {
                const child = createFakeChild();
                children.push(child);
                return child;
            });

            const mgr = new WorkerManager('/ext', logger, '/workspace', fakeWorkers, INSTANCE_ID);

            const first = mgr.ensureRunning('Worker.Workspace');
            children[0].stderr.write('[AutoContext.Worker.Workspace] Ready.\n');
            await new Promise(r => setImmediate(r));
            await expect(first).resolves.toBeUndefined();

            // Child exits after becoming ready — slot is cleared.
            children[0].emit('exit', 0);

            const second = mgr.ensureRunning('Worker.Workspace');
            expect(spawnMock).toHaveBeenCalledTimes(2);
            expect(second).not.toBe(first);

            children[1].stderr.write('[AutoContext.Worker.Workspace] Ready.\n');
            await new Promise(r => setImmediate(r));
            await expect(second).resolves.toBeUndefined();

            mgr.dispose();
        });

        it('should respawn after a previous spawn failed with an error', async () => {
            const children: FakeChild[] = [];
            spawnMock.mockImplementation(() => {
                const child = createFakeChild();
                children.push(child);
                return child;
            });

            const mgr = new WorkerManager('/ext', logger, '/workspace', fakeWorkers, INSTANCE_ID);

            const first = mgr.ensureRunning('Worker.Workspace');
            children[0].emit('error', new Error('spawn ENOENT'));
            await expect(first).rejects.toThrow(/spawn ENOENT/);

            const second = mgr.ensureRunning('Worker.Workspace');
            expect(spawnMock).toHaveBeenCalledTimes(2);
            expect(second).not.toBe(first);

            mgr.dispose();
            await expect(second).rejects.toThrow(/disposed/);
        });

        it('should reject after dispose without spawning', async () => {
            const mgr = new WorkerManager('/ext', logger, '/workspace', fakeWorkers, INSTANCE_ID);
            mgr.dispose();

            await expect(mgr.ensureRunning('Worker.Workspace')).rejects.toThrow(/disposed/);
            expect(spawnMock).not.toHaveBeenCalled();
        });
    });
});
