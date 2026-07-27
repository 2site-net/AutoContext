namespace AutoContext.Client.Core.Tests.Support.Engine.Rpc;

using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Binds one engine endpoint through the production
/// <see cref="PipeListener"/> / <see cref="BoundPipeListener"/> pair and
/// runs its accept loop, handing every accepted connection to a
/// caller-supplied handler. The pipe fakes compose it so their bind and
/// accept semantics match the engine's own — the same
/// <see cref="System.IO.Pipes.PipeOptions.FirstPipeInstance"/> ownership
/// and multi-accept backlog — rather than re-implementing a raw
/// <see cref="System.IO.Pipes.NamedPipeServerStream"/> accept loop. The
/// listener owns each accepted stream and disposes it after the handler
/// returns, and it isolates a handler fault from the accept loop.
/// </summary>
internal sealed class EnginePipeServerHarness : IAsyncDisposable
{
    private readonly Task _acceptLoop;
    private readonly CancellationTokenSource _cts = new();
    private readonly BoundPipeListener _listener;

    /// <summary>
    /// Binds <paramref name="pipeName"/> and starts the accept loop,
    /// invoking <paramref name="connectionHandler"/> once per accepted
    /// connection with the connected stream and the harness's token.
    /// </summary>
    /// <param name="pipeName">Endpoint address to bind. Must not be
    /// <see langword="null"/> or whitespace.</param>
    /// <param name="connectionHandler">Per-connection script. Must not
    /// dispose the supplied stream — the listener owns it.</param>
    public EnginePipeServerHarness(
        string pipeName, Func<Stream, CancellationToken, Task> connectionHandler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(connectionHandler);

        _listener = new PipeListener(pipeName, NullLogger<PipeListener>.Instance).Bind();
        _acceptLoop = _listener.RunAsync(connectionHandler, _cts.Token);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _acceptLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }

        await _listener.DisposeAsync().ConfigureAwait(false);
        _cts.Dispose();
    }
}
