namespace AutoContext.Mcp.Server.Workers;

using System.IO.Pipes;
using System.Text.Json;

using AutoContext.Mcp.Server.Workers.Control;
using AutoContext.Mcp.Server.Workers.Protocol;
using AutoContext.Mcp.Server.Workers.Transport;
using AutoContext.Framework.Workers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Writes one per-task request to a worker pipe and reads one response.
/// Enforces a single client-side wait deadline guarding against a hung
/// or dead worker. Any failure (connect, write, read, parse, timeout,
/// EOF) is mapped to an error <see cref="TaskResponse"/> rather than
/// thrown — callers compose these into the per-task entries of the
/// uniform tool-result envelope. Each handled failure is also logged at
/// <c>Warning</c> so server operators can see worker pipe issues even
/// though clients only see the synthesized error envelope.
/// </summary>
public sealed partial class WorkerClient
{
    /// <summary>Default wait deadline for connect + read.</summary>
    public static readonly TimeSpan DefaultWaitDeadline = TimeSpan.FromSeconds(30);

    private readonly TimeSpan _waitDeadline;
    private readonly EndpointOptions _endpoints;
    private readonly WorkerControlClient _workerControl;
    private readonly ILogger<WorkerClient> _logger;

    public WorkerClient()
        : this(DefaultWaitDeadline, new EndpointOptions(), WorkerControlClient.Disabled, NullLogger<WorkerClient>.Instance)
    {
    }

    public WorkerClient(TimeSpan waitDeadline)
        : this(waitDeadline, new EndpointOptions(), WorkerControlClient.Disabled, NullLogger<WorkerClient>.Instance)
    {
    }

    public WorkerClient(EndpointOptions endpoints)
        : this(DefaultWaitDeadline, endpoints, WorkerControlClient.Disabled, NullLogger<WorkerClient>.Instance)
    {
    }

    public WorkerClient(EndpointOptions endpoints, ILogger<WorkerClient> logger)
        : this(DefaultWaitDeadline, endpoints, WorkerControlClient.Disabled, logger)
    {
    }

    public WorkerClient(TimeSpan waitDeadline, EndpointOptions endpoints)
        : this(waitDeadline, endpoints, WorkerControlClient.Disabled, NullLogger<WorkerClient>.Instance)
    {
    }

    public WorkerClient(TimeSpan waitDeadline, EndpointOptions endpoints, ILogger<WorkerClient> logger)
        : this(waitDeadline, endpoints, WorkerControlClient.Disabled, logger)
    {
    }

    public WorkerClient(
        EndpointOptions endpoints,
        WorkerControlClient workerControl,
        ILogger<WorkerClient> logger)
        : this(DefaultWaitDeadline, endpoints, workerControl, logger)
    {
    }

    public WorkerClient(
        TimeSpan waitDeadline,
        EndpointOptions endpoints,
        WorkerControlClient workerControl,
        ILogger<WorkerClient> logger)
    {
        if (waitDeadline <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(waitDeadline),
                waitDeadline,
                "Wait deadline must be positive.");
        }

        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(workerControl);
        ArgumentNullException.ThrowIfNull(logger);

        _waitDeadline = waitDeadline;
        _endpoints = endpoints;
        _workerControl = workerControl;
        _logger = logger;
    }

    /// <summary>
    /// Connects to <paramref name="endpoint"/>, writes <paramref name="request"/>,
    /// and returns the worker's response — or a synthesized error response
    /// describing the pipe failure. Never throws for IO/timeout/parse
    /// failures; only throws for caller-side argument errors.
    /// </summary>
    public async Task<TaskResponse> InvokeAsync(
        string endpoint,
        TaskRequest request,
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
            // Ask the extension to ensure the worker is running before
            // we attempt to connect. No-op when the orchestrator was
            // launched without a --worker-control-pipe (standalone runs,
            // smoke tests). Failures are mapped to the same error
            // envelope shape we use for pipe IO failures so MCP callers
            // see a consistent contract.
            if (EndpointFormatter.TryParseId(endpoint, out var workerId))
            {
                await _workerControl.EnsureRunningAsync(workerId, token).ConfigureAwait(false);
            }

            return await InvokeCoreAsync(resolvedEndpoint, request, token).ConfigureAwait(false);
        }
        catch (WorkerControlException ex)
        {
            LogPipeFailure(_logger, request.McpTask, resolvedEndpoint, "worker control denied", ex);
            return ErrorResponse(
                request.McpTask,
                $"Worker control could not start '{ex.WorkerId}': {ex.Message}");
        }
        catch (OperationCanceledException) when (deadlineCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            LogDeadlineExceeded(_logger, request.McpTask, resolvedEndpoint, _waitDeadline.TotalSeconds);
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
            LogPipeFailure(_logger, request.McpTask, resolvedEndpoint, "connect timed out", ex);
            return ErrorResponse(
                request.McpTask,
                $"Pipe connect to '{resolvedEndpoint}' timed out: {ex.Message}");
        }
        catch (IOException ex)
        {
            LogPipeFailure(_logger, request.McpTask, resolvedEndpoint, "IO failure", ex);
            return ErrorResponse(
                request.McpTask,
                $"Pipe IO failure on '{resolvedEndpoint}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            LogPipeFailure(_logger, request.McpTask, resolvedEndpoint, "access denied", ex);
            return ErrorResponse(
                request.McpTask,
                $"Pipe access denied on '{resolvedEndpoint}': {ex.Message}");
        }
        catch (JsonException ex)
        {
            LogPipeFailure(_logger, request.McpTask, resolvedEndpoint, "response was not valid JSON", ex);
            return ErrorResponse(
                request.McpTask,
                $"Pipe response from '{resolvedEndpoint}' was not valid JSON: {ex.Message}");
        }
    }

    private static async Task<TaskResponse> InvokeCoreAsync(
        string endpoint,
        TaskRequest request,
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
                WorkerJsonOptions.Instance);

            var channel = new WorkerProtocolChannel(pipe);
            await channel.WriteAsync(requestBytes, token).ConfigureAwait(false);

            var responseBytes = await channel.ReadAsync(token).ConfigureAwait(false);

            if (responseBytes is null)
            {
                return ErrorResponse(
                    request.McpTask,
                    $"Worker on '{endpoint}' closed the pipe before sending a response.");
            }

            var response = JsonSerializer.Deserialize<TaskResponse>(
                responseBytes,
                WorkerJsonOptions.Instance);

            if (response is null)
            {
                return ErrorResponse(
                    request.McpTask,
                    $"Worker on '{endpoint}' returned a null response payload.");
            }

            return response;
        }
    }

    private static TaskResponse ErrorResponse(string mcpTask, string message) => new()
    {
        McpTask = mcpTask,
        Status = TaskResponse.StatusError,
        Output = null,
        Error = message,
    };

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Worker pipe call '{Task}' on '{Endpoint}' failed ({Reason}).")]
    private static partial void LogPipeFailure(ILogger logger, string task, string endpoint, string reason, Exception ex);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Worker pipe call '{Task}' on '{Endpoint}' exceeded the {DeadlineSeconds:0.##}s wait deadline.")]
    private static partial void LogDeadlineExceeded(ILogger logger, string task, string endpoint, double deadlineSeconds);
}
