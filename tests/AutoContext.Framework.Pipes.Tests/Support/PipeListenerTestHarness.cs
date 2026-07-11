namespace AutoContext.Framework.Pipes.Tests.Support;

using System.IO.Pipes;

using AutoContext.Framework.Pipes;

/// <summary>
/// Client-side helpers for driving a <see cref="BoundPipeListener"/> from
/// tests: creating clients, waiting for the first connection to be
/// accepted, and hammering a connect/disconnect burst at the accept loop.
/// Keeps the connect/drain plumbing out of the test bodies.
/// </summary>
public static class PipeListenerTestHarness
{
    /// <summary>
    /// Per-connect timeout so a starved pipe fails fast with a
    /// <see cref="TimeoutException"/> rather than hanging the run.
    /// </summary>
    public const int DefaultConnectTimeoutMs = 4_000;

    /// <summary>Creates an asynchronous byte-mode client for <paramref name="pipeName"/>.</summary>
    public static NamedPipeClientStream CreateClient(string pipeName) =>
        new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

    /// <summary>
    /// Runs <paramref name="listener"/>'s accept loop until the first
    /// connection reaches a handler, then stops it. Returns whether the
    /// handler fired <paramref name="within"/> the time budget.
    /// </summary>
    public static async Task<bool> WasFirstConnectionAcceptedAsync(
        BoundPipeListener listener, TimeSpan within, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(listener);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var accepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runTask = listener.RunAsync(
            (stream, token) => SignalThenDrainAsync(stream, accepted, token), cts.Token);

        try
        {
            await accepted.Task.WaitAsync(within, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            await cts.CancelAsync().ConfigureAwait(false);
            await runTask.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs <paramref name="listener"/>'s accept loop while a rapid
    /// connect/disconnect burst of <paramref name="cycles"/> hammers
    /// <paramref name="pipeName"/>, then stops the loop. Returns the fault
    /// the loop observed, or <see langword="null"/> if it drained cleanly.
    /// </summary>
    public static async Task<Exception?> CaptureAcceptLoopFaultDuringBurstAsync(
        BoundPipeListener listener, string pipeName, int cycles, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(listener);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var runTask = listener.RunAsync(DrainAsync, cts.Token);

        Exception? loopFault;
        try
        {
            await RunConnectDisconnectBurstAsync(pipeName, cycles, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Always stop the loop and observe it — disposing its server
            // streams — before cts is disposed. Record (never throw) the
            // fault so a burst failure in the try still propagates unmasked.
            await cts.CancelAsync().ConfigureAwait(false);
            loopFault = await Record.ExceptionAsync(() => runTask).ConfigureAwait(false);
        }

        return loopFault;
    }

    private static async Task RunConnectDisconnectBurstAsync(
        string pipeName, int cycles, CancellationToken cancellationToken)
    {
        for (var i = 0; i < cycles; i++)
        {
            var client = CreateClient(pipeName);

            await using (client.ConfigureAwait(false))
            {
                await client.ConnectAsync(DefaultConnectTimeoutMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task DrainAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1];

        while (await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) > 0)
        {
        }
    }

    private static Task SignalThenDrainAsync(
        Stream stream, TaskCompletionSource accepted, CancellationToken cancellationToken)
    {
        accepted.TrySetResult();
        return DrainAsync(stream, cancellationToken);
    }
}
