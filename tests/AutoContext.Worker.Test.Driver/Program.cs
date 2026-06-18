namespace AutoContext.Worker.Test.Driver;

using AutoContext.Engine.Protocol;
using AutoContext.Framework.Pipes;
using AutoContext.Worker.Test.Driver.Tasks;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// <c>AutoContext.Worker.Test.Driver</c> entry point. A deterministic,
/// behaviour-controllable worker that exists only to drive the engine
/// integration suite's <c>McpTools.Invoke</c> → worker-dispatch path end
/// to end. It serves three fixed tasks — <c>test_echo</c> (happy path),
/// <c>test_fail</c> (worker-reported failure), and <c>test_hang</c>
/// (cancellation) — over the engine's worker pipe contract.
/// </summary>
/// <remarks>
/// <para>
/// Standalone by design: it depends only on the engine's wire primitives
/// (<see cref="ServiceAddressFormatter"/> for its listen address and
/// <c>AutoContext.Framework.Pipes</c> for the pipe listener and frame
/// codec), never on <c>AutoContext.Framework.Workers</c>. That keeps the
/// driver decoupled from the worker-host framework and faithful to exactly
/// what the engine dials.
/// </para>
/// <para>
/// Readiness is a pipe probe on the engine side, so the driver emits no
/// stderr ready-marker: the listener is connectable the moment
/// <see cref="PipeListener.Bind"/> returns. The driver is never shipped —
/// it lives under <c>tests/</c>, so the build's worker-manifest generator
/// (which globs <c>src/AutoContext.Worker.*</c>) never aggregates it into
/// the engine's bundled <c>workers.json</c>; integration tests register it
/// explicitly.
/// </para>
/// </remarks>
internal static class Program
{
    /// <summary>
    /// Stable worker identifier. Formats this worker's listen address
    /// (<c>autocontext.worker-test-driver#&lt;instance-id&gt;</c>) and is the
    /// <c>workerId</c> a test registry's tools dispatch to.
    /// </summary>
    internal const string WorkerId = "test-driver";

    private const string InstanceIdSwitch = "--instance-id";

    internal static async Task Main(string[] args)
    {
        var instanceId = ParseSwitchValue(args, InstanceIdSwitch);
        var pipeName = ServiceAddressFormatter.Format($"worker-{WorkerId}", instanceId);

        var dispatcher = new TaskDispatcher(
        [
            new EchoTask(),
            new FailTask(),
            new HangTask(),
        ]);

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        var listener = new PipeListener(pipeName, NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();

        await using (bound.ConfigureAwait(false))
        {
            await bound.RunAsync(
                (stream, token) => HandleConnectionAsync(dispatcher, stream, token),
                shutdown.Token).ConfigureAwait(false);
        }
    }

    private static async Task HandleConnectionAsync(
        TaskDispatcher dispatcher,
        Stream stream,
        CancellationToken cancellationToken)
    {
        var codec = new LengthPrefixedFrameCodec(stream);

        var requestBytes = await codec.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (requestBytes is null)
        {
            return;
        }

        var responseBytes = await dispatcher.DispatchAsync(requestBytes, cancellationToken).ConfigureAwait(false);
        await codec.WriteAsync(responseBytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the value following <paramref name="name"/> in
    /// <paramref name="args"/>, or <see langword="null"/> when the switch is
    /// absent. The engine always supplies <c>--instance-id</c>; standalone
    /// runs omit it and fall back to the un-suffixed listen address.
    /// </summary>
    private static string? ParseSwitchValue(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}

