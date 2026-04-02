namespace SharpPilot.Mcp.DotNet.Tests.Fakes;

using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;

/// <summary>
/// Minimal named-pipe server that speaks the same length-prefixed binary
/// protocol as <c>SharpPilot.WorkspaceServer</c>. Returns canned responses
/// keyed by the <c>file-path</c> property in each request.
/// </summary>
internal sealed class FakeWorkspaceServer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
    };

    private readonly string _pipeName;
    private readonly Dictionary<string, Dictionary<string, string>> _responses = [];

    internal FakeWorkspaceServer(string pipeName)
    {
        _pipeName = pipeName;
    }

    /// <summary>
    /// Registers a canned response for the given file path.
    /// </summary>
    internal void SetResponse(string filePath, Dictionary<string, string> properties)
        => _responses[filePath] = properties;

    /// <summary>
    /// Runs the fake server loop until <paramref name="ct"/> is cancelled.
    /// </summary>
    internal async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            CancellationTokenRegistration registration = default;

            try
            {
                pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                registration = ct.Register(pipe.Dispose);

                await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);

                await using (pipe.ConfigureAwait(false))
                {
                    var requestBytes = await ReadMessageAsync(pipe, ct).ConfigureAwait(false);

                    if (requestBytes is null)
                    {
                        continue;
                    }

                    using var doc = JsonDocument.Parse(requestBytes);
                    var filePath = doc.RootElement.GetProperty("file-path").GetString() ?? string.Empty;

                    _responses.TryGetValue(filePath, out var properties);

                    var response = JsonSerializer.SerializeToUtf8Bytes(
                        new { properties = properties ?? [] },
                        JsonOptions);

                    await WriteMessageAsync(pipe, response, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (ct.IsCancellationRequested)
            {
                break;
            }
            finally
            {
                await registration.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task WriteMessageAsync(Stream stream, byte[] payload, CancellationToken ct)
    {
        var message = new byte[4 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(message, payload.Length);
        payload.CopyTo(message.AsSpan(4));

        await stream.WriteAsync(message, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task<byte[]?> ReadMessageAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[4];

        if (!await ReadExactAsync(stream, header, ct).ConfigureAwait(false))
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);

        if (length <= 0)
        {
            return [];
        }

        var payload = new byte[length];

        return await ReadExactAsync(stream, payload, ct).ConfigureAwait(false) ? payload : null;
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;

        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(offset, buffer.Length - offset), ct).ConfigureAwait(false);

            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }
}
