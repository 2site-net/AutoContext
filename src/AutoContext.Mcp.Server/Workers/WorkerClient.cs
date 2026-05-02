namespace AutoContext.Mcp.Server.Workers;

using System.Text.Json;

using AutoContext.Mcp.Server.Workers.Control;
using AutoContext.Mcp.Server.Workers.Protocol;
using AutoContext.Mcp.Server.Workers.Transport;
using AutoContext.Framework.Pipes;

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
    private readonly ServiceAddressOptions _addresses;
    private readonly WorkerControlClient _workerControl;
    private readonly ILogger<WorkerClient> _logger;

    public WorkerClient()
        : this(DefaultWaitDeadline, new ServiceAddressOptions(), WorkerControlClient.Disabled, NullLogger<WorkerClient>.Instance)
    {
    }

    public WorkerClient(TimeSpan waitDeadline)
        : this(waitDeadline, new ServiceAddressOptions(), WorkerControlClient.Disabled, NullLogger<WorkerClient>.Instance)
    {
    }

    public WorkerClient(ServiceAddressOptions addresses)
        : this(DefaultWaitDeadline, addresses, WorkerControlClient.Disabled, NullLogger<WorkerClient>.Instance)
    {
    }

    public WorkerClient(ServiceAddressOptions addresses, ILogger<WorkerClient> logger)
        : this(DefaultWaitDeadline, addresses, WorkerControlClient.Disabled, logger)
    {
    }

    public WorkerClient(TimeSpan waitDeadline, ServiceAddressOptions addresses)
        : this(waitDeadline, addresses, WorkerControlClient.Disabled, NullLogger<WorkerClient>.Instance)
    {
    }

    public WorkerClient(TimeSpan waitDeadline, ServiceAddressOptions addresses, ILogger<WorkerClient> logger)
        : this(waitDeadline, addresses, WorkerControlClient.Disabled, logger)
    {
    }

    public WorkerClient(
        ServiceAddressOptions addresses,
        WorkerControlClient workerControl,
        ILogger<WorkerClient> logger)
        : this(DefaultWaitDeadline, addresses, workerControl, logger)
    {
    }

    public WorkerClient(
        TimeSpan waitDeadline,
        ServiceAddressOptions addresses,
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

        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(workerControl);
        ArgumentNullException.ThrowIfNull(logger);

        _waitDeadline = waitDeadline;
        _addresses = addresses;
        _workerControl = workerControl;
        _logger = logger;
    }

    /// <summary>
    /// Connects to the pipe address derived from <paramref name="role"/>
    /// (e.g. <c>"worker-dotnet"</c>), writes <paramref name="request"/>,
    /// and returns the worker's response — or a synthesized error response
    /// describing the pipe failure. Never throws for IO/timeout/parse
    /// failures; only throws for caller-side argument errors.
    /// </summary>
    public async Task<TaskResponse> InvokeAsync(
        string role,
        TaskRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(role);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.McpTask);

        var address = _addresses.Format(role);

        using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineCts.CancelAfter(_waitDeadline);
        var token = deadlineCts.Token;

        try
        {
            // Ask the extension to ensure the worker is running before
            // we attempt to connect. No-op when the orchestrator was
            // launched without a worker-control service address
            // (standalone runs, smoke tests). Failures are mapped to
            // the same error envelope shape we use for pipe IO
            // failures so MCP callers see a consistent contract.
            const string WorkerRolePrefix = "worker-";
            if (role.StartsWith(WorkerRolePrefix, StringComparison.Ordinal))
            {
                var workerId = role[WorkerRolePrefix.Length..];
                if (workerId.Length > 0)
                {
                    await _workerControl.EnsureRunningAsync(workerId, token).ConfigureAwait(false);
                }
            }

            return await InvokeCoreAsync(address, request, token).ConfigureAwait(false);
        }
        catch (WorkerControlException ex)
        {
            LogPipeFailure(_logger, request.McpTask, address, "worker control denied", ex);
            return ErrorResponse(
                request.McpTask,
                $"Worker control could not start '{ex.WorkerId}': {ex.Message}");
        }
        catch (OperationCanceledException) when (deadlineCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            LogDeadlineExceeded(_logger, request.McpTask, address, _waitDeadline.TotalSeconds);
            return ErrorResponse(
                request.McpTask,
                $"Pipe call to '{address}' exceeded the {_waitDeadline.TotalSeconds:0.##}s wait deadline.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            LogPipeFailure(_logger, request.McpTask, address, "connect timed out", ex);
            return ErrorResponse(
                request.McpTask,
                $"Pipe connect to '{address}' timed out: {ex.Message}");
        }
        catch (IOException ex)
        {
            LogPipeFailure(_logger, request.McpTask, address, "IO failure", ex);
            return ErrorResponse(
                request.McpTask,
                $"Pipe IO failure on '{address}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            LogPipeFailure(_logger, request.McpTask, address, "access denied", ex);
            return ErrorResponse(
                request.McpTask,
                $"Pipe access denied on '{address}': {ex.Message}");
        }
        catch (JsonException ex)
        {
            LogPipeFailure(_logger, request.McpTask, address, "response was not valid JSON", ex);
            return ErrorResponse(
                request.McpTask,
                $"Pipe response from '{address}' was not valid JSON: {ex.Message}");
        }
    }

    private static async Task<TaskResponse> InvokeCoreAsync(
        string address,
        TaskRequest request,
        CancellationToken token)
    {
        var transport = new PipeTransport(NullLogger<PipeTransport>.Instance);
        var exchange = new PipeTransientExchangeClient(transport, address);
        await using var _ = exchange.ConfigureAwait(false);

        var requestBytes = JsonSerializer.SerializeToUtf8Bytes(
            request,
            WorkerJsonOptions.Instance);

        var responseBytes = await exchange.ExchangeAsync(requestBytes, token).ConfigureAwait(false);

        var response = JsonSerializer.Deserialize<TaskResponse>(
            responseBytes,
            WorkerJsonOptions.Instance);

        if (response is null)
        {
            return ErrorResponse(
                request.McpTask,
                $"Worker on '{address}' returned a null response payload.");
        }

        return response;
    }

    private static TaskResponse ErrorResponse(string mcpTask, string message) => new()
    {
        McpTask = mcpTask,
        Status = TaskResponse.StatusError,
        Output = null,
        Error = message,
    };

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Worker pipe call '{Task}' on '{Address}' failed ({Reason}).")]
    private static partial void LogPipeFailure(ILogger logger, string task, string address, string reason, Exception ex);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Worker pipe call '{Task}' on '{Address}' exceeded the {DeadlineSeconds:0.##}s wait deadline.")]
    private static partial void LogDeadlineExceeded(ILogger logger, string task, string address, double deadlineSeconds);
}
