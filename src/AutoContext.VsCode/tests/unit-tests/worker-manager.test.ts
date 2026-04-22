import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { EventEmitter } from 'node:events';
import { PassThrough } from 'node:stream';
import { createFakeOutputChannel } from './_fakes';

const spawnMock = vi.fn();

vi.mock('node:child_process', () => ({
    spawn: (...args: unknown[]) => spawnMock(...args),
}));

// Import after mocks so the module picks up the mocked spawn.
const { WorkerManager } = await import('../../src/worker-manager');

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

describe('WorkerManager', () => {
    const outputChannel = createFakeOutputChannel();
    const appendLine = outputChannel.appendLine as unknown as ReturnType<typeof vi.fn>;
    let manager: InstanceType<typeof WorkerManager>;

    beforeEach(() => {
        vi.clearAllMocks();
        spawnMock.mockImplementation(() => createFakeChild());
        manager = new WorkerManager('/ext', outputChannel, '/workspace');
    });

    afterEach(() => {
        manager.dispose();
    });

    it('should generate a 12-character hex endpoint suffix', () => {
        expect(manager.getEndpointSuffix()).toMatch(/^[0-9a-f]{12}$/);
    });

    it('should give a distinct suffix per instance', () => {
        const other = new WorkerManager('/ext', outputChannel, '/workspace');

        expect(other.getEndpointSuffix()).not.toBe(manager.getEndpointSuffix());

        other.dispose();
    });

    it('should spawn three worker processes on start', () => {
        manager.start();

        expect(spawnMock).toHaveBeenCalledTimes(3);
    });

    it('should be idempotent', () => {
        manager.start();
        manager.start();

        expect(spawnMock).toHaveBeenCalledTimes(3);
    });

    it('should pass --pipe with the suffixed pipe name to every worker', () => {
        manager.start();

        const suffix = manager.getEndpointSuffix();
        const commands = spawnMock.mock.calls.map(c => ({ cmd: c[0] as string, args: c[1] as string[] }));
        const workspaceCall = commands.find(c => c.cmd.includes('Worker.Workspace'));
        const dotnetCall = commands.find(c => c.cmd.includes('Worker.DotNet'));
        const webCall = commands.find(c => c.cmd === 'node');

        expect(workspaceCall?.args).toContain(`autocontext.workspace-worker-${suffix}`);
        expect(dotnetCall?.args).toContain(`autocontext.dotnet-worker-${suffix}`);
        expect(webCall?.args).toContain(`autocontext.web-worker-${suffix}`);
    });

    it('should pass --workspace-root to Worker.Workspace when provided', () => {
        manager.start();

        const workspaceCall = spawnMock.mock.calls.find(c => (c[0] as string).includes('Worker.Workspace'));

        expect(workspaceCall?.[1]).toContain('--workspace-root');
        expect(workspaceCall?.[1]).toContain('/workspace');
    });

    it('should omit --workspace-root when no workspace root is given', () => {
        const mgr = new WorkerManager('/ext', outputChannel, undefined);
        mgr.start();

        const workspaceCall = spawnMock.mock.calls.find(c => (c[0] as string).includes('Worker.Workspace'));

        expect(workspaceCall?.[1]).not.toContain('--workspace-root');

        mgr.dispose();
    });

    it('should forward stderr lines to the output channel with a process-identity prefix', async () => {
        const children: FakeChild[] = [];
        spawnMock.mockImplementation(() => {
            const child = createFakeChild();
            children.push(child);
            return child;
        });

        const mgr = new WorkerManager('/ext', outputChannel, '/workspace');
        mgr.start();
        const workspaceChild = children[0];
        workspaceChild.stderr.write('hello from workspace\n');

        await new Promise(r => setImmediate(r));

        expect(appendLine).toHaveBeenCalledWith('[Worker.Workspace] hello from workspace');

        mgr.dispose();
    });

    it('should resolve whenWorkspaceReady when the workspace ready marker is observed', async () => {
        const children: FakeChild[] = [];
        spawnMock.mockImplementation(() => {
            const child = createFakeChild();
            children.push(child);
            return child;
        });

        const mgr = new WorkerManager('/ext', outputChannel, '/workspace');
        mgr.start();
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

        const mgr = new WorkerManager('/ext', outputChannel, '/workspace');
        mgr.start();
        mgr.dispose();

        expect(children).toHaveLength(3);
        expect(children.every(c => c.kill.mock.calls.length === 1)).toBe(true);
    });
});
