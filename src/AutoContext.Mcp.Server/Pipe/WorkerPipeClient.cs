namespace AutoContext.Mcp.Server.Pipe;

using System.IO.Pipes;
using System.Text.Json;

using AutoContext.Mcp.Server.Wire;
using AutoContext.Worker.Hosting;

/// <summary>
/// Writes one per-task wire request to a worker pipe and reads one wire
/// response. Enforces a single client-side wait deadline guarding against
/// a hung or dead worker. Any failure (connect, write, read, parse,
/// timeout, EOF) is mapped to an error <see cref="TaskWireResponse"/>
/// rather than thrown — callers compose these into the per-task entries
/// of the uniform tool-result envelope.
/// </summary>
public sealed class WorkerPipeClient
{
    /// <summary>Default wait deadline for connect + read.</summary>
    public static readonly TimeSpan DefaultWaitDeadline = TimeSpan.FromSeconds(30);

    private readonly TimeSpan _waitDeadline;
    private readonly EndpointOptions _endpoints;

    public WorkerPipeClient()
        : this(DefaultWaitDeadline, new EndpointOptions())
    {
    }

    public WorkerPipeClient(TimeSpan waitDeadline)
        : this(waitDeadline, new EndpointOptions())
    {
    }

    public WorkerPipeClient(EndpointOptions endpoints)
        : this(DefaultWaitDeadline, endpoints)
    {
    }

    public WorkerPipeClient(TimeSpan waitDeadline, EndpointOptions endpoints)
    {
        if (waitDeadline <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(waitDeadline),
                waitDeadline,
                "Wait deadline must be positive.");
        }

        ArgumentNullException.ThrowIfNull(endpoints);

        _waitDeadline = waitDeadline;
        _endpoints = endpoints;
    }

    /// <summary>
    /// Connects to <paramref name="endpoint"/>, writes <paramref name="request"/>,
    /// and returns the worker's response — or a synthesized error response
    /// describing the pipe failure. Never throws for IO/timeout/parse
    /// failures; only throws for caller-side argument errors.
    /// </summary>
    public async Task<TaskWireResponse> InvokeAsync(
        string endpoint,
        TaskWireRequest request,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(endpoint);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.McpTask);

        var resolvedEndpoint = _endpoints.Resolve(endpoint);

        using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadlineCts.CancelAfter(_waitDeadline);
        var token = deadlineCts.Token;

        try
        {
            return await InvokeCoreAsync(resolvedEndpoint, request, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (deadlineCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return ErrorResponse(
                request.McpTask,
                $"Pipe call to '{resolvedEndpoint}' exceeded the {_waitDeadline.TotalSeconds:0.##}s wait deadline.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            return ErrorResponse(
                request.McpTask,
                $"Pipe connect to '{resolvedEndpoint}' timed out: {ex.Message}");
        }
        catch (IOException ex)
        {
            return ErrorResponse(
                request.McpTask,
                $"Pipe IO failure on '{resolvedEndpoint}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return ErrorResponse(
                request.McpTask,
                $"Pipe access denied on '{resolvedEndpoint}': {ex.Message}");
        }
        catch (JsonException ex)
        {
            return ErrorResponse(
                request.McpTask,
                $"Pipe response from '{resolvedEndpoint}' was not valid JSON: {ex.Message}");
        }
    }

    private static async Task<TaskWireResponse> InvokeCoreAsync(
        string endpoint,
        TaskWireRequest request,
        CancellationToken token)
    {
        var pipe = new NamedPipeClientStream(
            ".",
            endpoint,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await using (pipe.ConfigureAwait(false))
        {
            await pipe.ConnectAsync(token).ConfigureAwait(false);

            var requestBytes = JsonSerializer.SerializeToUtf8Bytes(
                request,
                WireJsonOptions.Instance);

            await PipeFraming.WriteMessageAsync(pipe, requestBytes, token).ConfigureAwait(false);

            var responseBytes = await PipeFraming.ReadMessageAsync(pipe, token).ConfigureAwait(false);

            if (responseBytes is null)
            {
                return ErrorResponse(
                    request.McpTask,
                    $"Worker on '{endpoint}' closed the pipe before sending a response.");
            }

            var response = JsonSerializer.Deserialize<TaskWireResponse>(
                responseBytes,
                WireJsonOptions.Instance);

            if (response is null)
            {
                return ErrorResponse(
                    request.McpTask,
                    $"Worker on '{endpoint}' returned a null response payload.");
            }

            return response;
        }
    }

    private static TaskWireResponse ErrorResponse(string mcpTask, string message) => new()
    {
        McpTask = mcpTask,
        Status = TaskWireResponse.StatusError,
        Output = null,
        Error = message,
    };
}
