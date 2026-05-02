import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { connect, type Socket } from 'node:net';
import { LogServer } from '#src/log-server';
import { createFakeLogger } from '#testing/fakes';
import { pipePath } from '#testing/utils/pipe-helpers';
import { waitFor } from '#testing/utils/wait-for';
import type { ChannelLogger } from 'autocontext-framework-web';

/**
 * Connects to the LogServer pipe, sends the greeting + an arbitrary
 * sequence of NDJSON record bodies, and resolves the socket once every
 * line has been flushed.
 */
async function connectAndSend(pipe: string, clientName: string, records: object[]): Promise<Socket> {
    return new Promise((resolve, reject) => {
        const socket = connect(pipePath(pipe), () => {
            const lines = [JSON.stringify({ clientName }), ...records.map(r => JSON.stringify(r))];
            const payload = lines.map(l => l + '\n').join('');
            socket.write(payload, (err) => err ? reject(err) : resolve(socket));
        });
        socket.on('error', reject);
    });
}

/**
 * Creates a fake logger whose `forChannel`/`forCategory` return distinct
 * children with their own spies — needed so the assertions can target
 * the per-worker / per-category child rather than the root.
 */
function createTreeLogger(): { root: ChannelLogger; channels: Map<string, ChannelLogger>; categories: Map<string, ChannelLogger> } {
    const channels = new Map<string, ChannelLogger>();
    const categories = new Map<string, ChannelLogger>();

    const buildChild = (registry: Map<string, ChannelLogger>, key: string): ChannelLogger => {
        const existing = registry.get(key);
        if (existing) { return existing; }
        const child = createFakeLogger();
        // Children's forCategory should also produce per-category children
        // so per-record category dispatch is observable.
        (child.forCategory as ReturnType<typeof vi.fn>).mockImplementation((cat: string) => buildChild(categories, `${key}::${cat}`));
        (child.forChannel as ReturnType<typeof vi.fn>).mockImplementation((name: string) => buildChild(channels, name));
        registry.set(key, child);
        return child;
    };

    const root = createFakeLogger();
    (root.forChannel as ReturnType<typeof vi.fn>).mockImplementation((name: string) => buildChild(channels, name));
    (root.forCategory as ReturnType<typeof vi.fn>).mockImplementation((cat: string) => buildChild(categories, cat));

    return { root, channels, categories };
}

describe('LogServer', () => {
    let server: LogServer;
    let tree: ReturnType<typeof createTreeLogger>;

    beforeEach(async () => {
        vi.clearAllMocks();
        tree = createTreeLogger();
        server = new LogServer(tree.root, '0123456789ab');
        await server.start();
    });

    afterEach(() => {
        server.dispose();
    });

    it('should expose a deterministic pipe name', () => {
        expect(server.getPipeName()).toMatch(/^autocontext\.log#[a-f0-9]{12}$/);
    });

    it('should route records to a per-worker channel keyed by greeting clientName', async () => {
        const socket = await connectAndSend(server.getPipeName(), 'AutoContext.Worker.DotNet', [
            { category: 'Acme.Demo', level: 'Information', message: 'hello world' },
        ]);

        await waitFor(() => tree.channels.has('AutoContext: Worker.DotNet'));
        const workerChannel = tree.channels.get('AutoContext: Worker.DotNet')!;

        await waitFor(() =>
            (workerChannel.forCategory as ReturnType<typeof vi.fn>).mock.calls.length > 0);

        const categoryChild = tree.categories.get('AutoContext: Worker.DotNet::Acme.Demo')!;

        expect(categoryChild).toBeDefined();
        expect(categoryChild.info).toHaveBeenCalledExactlyOnceWith('hello world', undefined);

        socket.destroy();
    });

    it.each([
        ['Trace', 'trace'],
        ['Debug', 'debug'],
        ['Information', 'info'],
        ['Warning', 'warn'],
        ['Error', 'error'],
        ['Critical', 'error'],
    ] as const)('should map .NET %s to logger.%s', async (wireLevel, expectedMethod) => {
        const socket = await connectAndSend(server.getPipeName(), 'AutoContext.Worker.DotNet', [
            { category: 'Cat', level: wireLevel, message: `m-${wireLevel}` },
        ]);

        await waitFor(() => tree.categories.has('AutoContext: Worker.DotNet::Cat'));
        const categoryChild = tree.categories.get('AutoContext: Worker.DotNet::Cat')!;

        await waitFor(() =>
            (categoryChild[expectedMethod] as ReturnType<typeof vi.fn>).mock.calls.length > 0);

        expect(categoryChild[expectedMethod]).toHaveBeenCalledExactlyOnceWith(`m-${wireLevel}`, undefined);

        socket.destroy();
    });

    it('should forward exception text alongside the message', async () => {
        const socket = await connectAndSend(server.getPipeName(), 'AutoContext.Worker.DotNet', [
            {
                category: 'Cat',
                level: 'Error',
                message: 'oh no',
                exception: 'System.InvalidOperationException: boom\n   at X()',
            },
        ]);

        await waitFor(() => tree.categories.has('AutoContext: Worker.DotNet::Cat'));
        const categoryChild = tree.categories.get('AutoContext: Worker.DotNet::Cat')!;

        await waitFor(() =>
            (categoryChild.error as ReturnType<typeof vi.fn>).mock.calls.length > 0);

        expect(categoryChild.error).toHaveBeenCalledExactlyOnceWith(
            'oh no',
            'System.InvalidOperationException: boom\n   at X()',
        );

        socket.destroy();
    });

    it('should prefix the message with the correlation id when present', async () => {
        const socket = await connectAndSend(server.getPipeName(), 'AutoContext.Worker.DotNet', [
            { category: 'Cat', level: 'Information', message: 'scoped', correlationId: 'abcd1234' },
        ]);

        await waitFor(() => tree.categories.has('AutoContext: Worker.DotNet::Cat'));
        const categoryChild = tree.categories.get('AutoContext: Worker.DotNet::Cat')!;

        await waitFor(() =>
            (categoryChild.info as ReturnType<typeof vi.fn>).mock.calls.length > 0);

        expect(categoryChild.info).toHaveBeenCalledExactlyOnceWith('[abcd1234] scoped', undefined);

        socket.destroy();
    });

    it('should ignore records that arrive before the greeting', async () => {
        const socket = await new Promise<Socket>((resolve, reject) => {
            const s = connect(pipePath(server.getPipeName()), () => {
                s.write(JSON.stringify({ category: 'C', level: 'Information', message: 'orphan' }) + '\n',
                    (err) => err ? reject(err) : resolve(s));
            });
            s.on('error', reject);
        });

        // Give the server a moment to process. No worker channel should appear.
        await new Promise(r => setTimeout(r, 50));

        // No per-worker channel should have been created — the orphan
        // record was dropped before any greeting was processed.
        expect(tree.channels.size).toBe(0);
        // The root logger's `warn` should have been called with the
        // "Expected greeting line" message.
        expect(tree.root.warn).toHaveBeenCalled();

        socket.destroy();
    });

    it('should warn and continue on a malformed JSON line', async () => {
        const socket = await new Promise<Socket>((resolve, reject) => {
            const s = connect(pipePath(server.getPipeName()), () => {
                s.write(JSON.stringify({ clientName: 'AutoContext.Worker.DotNet' }) + '\n');
                s.write('not-valid-json\n');
                s.write(JSON.stringify({ category: 'Cat', level: 'Information', message: 'after' }) + '\n',
                    (err) => err ? reject(err) : resolve(s));
            });
            s.on('error', reject);
        });

        await waitFor(() => tree.categories.has('AutoContext: Worker.DotNet::Cat'));
        const categoryChild = tree.categories.get('AutoContext: Worker.DotNet::Cat')!;

        await waitFor(() =>
            (categoryChild.info as ReturnType<typeof vi.fn>).mock.calls.length > 0);

        expect(categoryChild.info).toHaveBeenCalledExactlyOnceWith('after', undefined);
        expect(tree.root.warn).toHaveBeenCalled();

        socket.destroy();
    });

    it('should support multiple concurrent worker connections with independent channels', async () => {
        const s1 = await connectAndSend(server.getPipeName(), 'AutoContext.Worker.DotNet', [
            { category: 'Cat', level: 'Information', message: 'from-dotnet' },
        ]);
        const s2 = await connectAndSend(server.getPipeName(), 'AutoContext.Worker.Workspace', [
            { category: 'Cat', level: 'Warning', message: 'from-workspace' },
        ]);

        await waitFor(() => tree.channels.has('AutoContext: Worker.DotNet')
            && tree.channels.has('AutoContext: Worker.Workspace'));

        const dotnetCat = tree.categories.get('AutoContext: Worker.DotNet::Cat');
        const workspaceCat = tree.categories.get('AutoContext: Worker.Workspace::Cat');

        await waitFor(() => dotnetCat !== undefined && workspaceCat !== undefined
            && (dotnetCat.info as ReturnType<typeof vi.fn>).mock.calls.length > 0
            && (workspaceCat!.warn as ReturnType<typeof vi.fn>).mock.calls.length > 0);

        expect(dotnetCat!.info).toHaveBeenCalledExactlyOnceWith('from-dotnet', undefined);
        expect(workspaceCat!.warn).toHaveBeenCalledExactlyOnceWith('from-workspace', undefined);

        s1.destroy();
        s2.destroy();
    });
});
