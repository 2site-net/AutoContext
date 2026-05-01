namespace AutoContext.Framework.Tests.Testing.Utils;

using System.IO.Pipes;

/// <summary>
/// Tiny helpers for tests that impersonate a named-pipe server and
/// drive client-side production code (e.g. <c>LoggingClient</c>,
/// <c>HealthMonitorClient</c>) inline. Consolidates the
/// <see cref="NamedPipeServerStream"/> constructor defaults the
/// production listeners use so individual tests don't drift.
/// </summary>
internal static class TestPipeServer
{
    /// <summary>
    /// Creates a single-instance server with the framework's
    /// production defaults (<see cref="PipeTransmissionMode.Byte"/>,
    /// <see cref="PipeOptions.Asynchronous"/>). The caller awaits
    /// <see cref="NamedPipeServerStream.WaitForConnectionAsync(CancellationToken)"/>
    /// and then reads/writes inline.
    /// </summary>
    public static NamedPipeServerStream Create(string pipeName, PipeDirection direction = PipeDirection.InOut) =>
        new(
            pipeName,
            direction,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

    /// <summary>
    /// Returns a unique 32-char pipe name. Pipe names are limited to
    /// 256 chars on Windows but UDS paths on POSIX truncate at ~104,
    /// so 32 keeps headroom under both.
    /// </summary>
    public static string UniqueName(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}"[..32];
}
