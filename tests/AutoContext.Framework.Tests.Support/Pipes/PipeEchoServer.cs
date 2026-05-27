namespace AutoContext.Framework.Tests.Support.Pipes;

using AutoContext.Framework.Pipes;

using AutoContext.Framework.Tests.Support.Encodings;

/// <summary>
/// Length-prefixed echo server used by the pipe exchange tests.
/// Reads a UTF-8 payload framed by <see cref="LengthPrefixedFrameCodec"/>
/// and writes <c>"pong:" + payload</c> back on the same stream.
/// </summary>
public static class PipeEchoServer
{
    /// <summary>
    /// Reads a single request frame, writes the matching <c>pong:</c>
    /// echo, and returns <c>true</c>. Returns <c>false</c> when the
    /// peer closed the stream before sending another frame.
    /// </summary>
    public static async Task<bool> EchoOnceAsync(Stream stream, CancellationToken cancellationToken)
    {
        var codec = new LengthPrefixedFrameCodec(stream);

        var request = await codec.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return false;
        }

        var response = TestEncodings.Utf8NoBom.GetBytes("pong:" + TestEncodings.Utf8NoBom.GetString(request));
        await codec.WriteAsync(response, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Echoes frames in a loop until the peer closes the stream or
    /// <paramref name="cancellationToken"/> is signalled.
    /// </summary>
    public static async Task EchoLoopAsync(Stream stream, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested
            && await EchoOnceAsync(stream, cancellationToken).ConfigureAwait(false))
        {
        }
    }
}
