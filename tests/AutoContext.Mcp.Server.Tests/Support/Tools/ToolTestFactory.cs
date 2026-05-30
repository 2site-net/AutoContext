namespace AutoContext.Mcp.Server.Tests.Support.Tools;

using System.Text.Json;

using AutoContext.Mcp.Server.Config;
using AutoContext.Mcp.Server.EditorConfig;
using AutoContext.Mcp.Server.Registry;
using AutoContext.Mcp.Server.Tools.Invocation;
using AutoContext.Mcp.Server.Workers;
using AutoContext.Mcp.Server.Workers.Protocol;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Shared SUT + fixture builders for the
/// <see cref="ToolInvoker"/> / <c>McpSdkAdapter</c> /
/// <see cref="EditorConfigBatcher"/> test suites. Centralises the
/// constructor wiring + <see cref="JsonMcpTaskDefinition"/> shape that
/// multiple test classes used to duplicate.
/// </summary>
internal static class ToolTestFactory
{
    private const string UnusedWorkspaceId = "autocontext-test-workspace-unused";

    /// <summary>
    /// Builds a <see cref="ToolInvoker"/> wired to a fresh
    /// <see cref="WorkerClient"/> + <see cref="EditorConfigBatcher"/>
    /// with the unused-workspace id every test uses.
    /// </summary>
    public static ToolInvoker BuildInvoker(
        AutoContextConfigSnapshot? configSnapshot = null,
        ILogger<ToolInvoker>? logger = null)
    {
        var workerClient = new WorkerClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(
            workerClient,
            UnusedWorkspaceId,
            NullLogger<EditorConfigBatcher>.Instance);

        return new ToolInvoker(
            workerClient,
            batcher,
            configSnapshot,
            logger ?? NullLogger<ToolInvoker>.Instance);
    }

    /// <summary>
    /// Builds an <see cref="JsonMcpTaskDefinition"/> with the given task
    /// name and optional <c>EditorConfig</c> keys.
    /// </summary>
    public static JsonMcpTaskDefinition BuildTask(
        string name,
        params string[] editorConfig) => new()
        {
            Name = name,
            EditorConfig = editorConfig,
        };

    /// <summary>
    /// Builds an <see cref="JsonMcpToolDefinition"/> with a single default
    /// <c>{name}_task</c> task (used by <c>McpSdkAdapter</c> snapshot
    /// filter tests).
    /// </summary>
    public static JsonMcpToolDefinition BuildTool(string name) => new()
    {
        Name = name,
        Description = "Test tool.",
        Parameters = new Dictionary<string, JsonMcpToolParameter>(StringComparer.Ordinal),
        Tasks = [new JsonMcpTaskDefinition { Name = $"{name}_task" }],
    };

    /// <summary>
    /// Builds an <see cref="JsonMcpToolDefinition"/> with explicit task
    /// definitions (used by <c>ToolInvoker</c> tests that need control
    /// over <c>EditorConfig</c> keys per task).
    /// </summary>
    public static JsonMcpToolDefinition BuildTool(
        string name,
        params JsonMcpTaskDefinition[] tasks) => new()
        {
            Name = name,
            Description = "Test tool.",
            Parameters = new Dictionary<string, JsonMcpToolParameter>(StringComparer.Ordinal),
            Tasks = tasks,
        };

    /// <summary>
    /// Builds an <see cref="JsonMcpToolDefinition"/> from a sequence of
    /// task names (used by <c>ToolDelegateFactory</c> tests).
    /// </summary>
    public static JsonMcpToolDefinition BuildToolFromTaskNames(
        string name,
        params string[] taskNames)
    {
        var tasks = new List<JsonMcpTaskDefinition>(taskNames.Length);

        foreach (var taskName in taskNames)
        {
            tasks.Add(new JsonMcpTaskDefinition { Name = taskName });
        }

        return new JsonMcpToolDefinition
        {
            Name = name,
            Description = "Test tool.",
            Parameters = new Dictionary<string, JsonMcpToolParameter>(StringComparer.Ordinal),
            Tasks = tasks,
        };
    }

    /// <summary>
    /// Builds an <see cref="JsonMcpWorker"/> with the given id and a
    /// placeholder name/empty tool list.
    /// </summary>
    public static JsonMcpWorker BuildWorker(string workerId) => new()
    {
        Id = workerId,
        Name = "AutoContext.Worker.Test",
        Tools = [],
    };

    /// <summary>
    /// Builds an <see cref="JsonMcpWorkersCatalog"/> from a sequence of
    /// (id, tools) tuples — worker name defaults to
    /// <c>AutoContext.Worker.{id}</c>.
    /// </summary>
    public static JsonMcpWorkersCatalog BuildCatalog(
        params (string Id, IReadOnlyList<JsonMcpToolDefinition> Definitions)[] workers)
    {
        var list = new List<JsonMcpWorker>(workers.Length);

        foreach (var (id, definitions) in workers)
        {
            list.Add(new JsonMcpWorker
            {
                Id = id,
                Name = $"AutoContext.Worker.{id}",
                Tools = definitions,
            });
        }

        return new JsonMcpWorkersCatalog
        {
            SchemaVersion = "1",
            Workers = list,
        };
    }

    /// <summary>
    /// Builds an <see cref="JsonMcpWorkersCatalog"/> from a sequence of
    /// (id, name, tools) tuples — used by tests that pin the worker
    /// name (e.g. process-spawn dispatch tests).
    /// </summary>
    public static JsonMcpWorkersCatalog BuildCatalog(
        params (string Id, string Name, IReadOnlyList<JsonMcpToolDefinition> Definitions)[] workers)
    {
        var list = new List<JsonMcpWorker>(workers.Length);

        foreach (var (id, name, definitions) in workers)
        {
            list.Add(new JsonMcpWorker
            {
                Id = id,
                Name = name,
                Tools = definitions,
            });
        }

        return new JsonMcpWorkersCatalog
        {
            SchemaVersion = "1",
            Workers = list,
        };
    }

    /// <summary>
    /// Returns an empty <see cref="JsonElement"/> object — handy as a
    /// placeholder <see cref="JsonTaskRequest.Data"/> for tests that don't
    /// care about request payload shape.
    /// </summary>
    public static JsonElement EmptyData() =>
        JsonSerializer.SerializeToElement(new { }, WorkerJsonOptions.Instance);

    /// <summary>
    /// Composes a length-framed OK <see cref="JsonTaskResponse"/> from a
    /// serialized <see cref="JsonTaskRequest"/> + an arbitrary output
    /// payload. The response's <c>McpTask</c> mirrors the request's.
    /// </summary>
    public static byte[] OkResponse(byte[] requestBytes, object output)
    {
        var request = JsonSerializer.Deserialize<JsonTaskRequest>(
            requestBytes,
            WorkerJsonOptions.Instance)!;

        var response = new JsonTaskResponse
        {
            McpTask = request.McpTask,
            Status = JsonTaskResponse.StatusOk,
            Output = JsonSerializer.SerializeToElement(output, WorkerJsonOptions.Instance),
            Error = string.Empty,
        };

        return JsonSerializer.SerializeToUtf8Bytes(response, WorkerJsonOptions.Instance);
    }

    /// <summary>
    /// Composes a length-framed OK <see cref="JsonTaskResponse"/> whose
    /// output is <c>{ servedBy = servedBy }</c> — used by tests that
    /// verify which worker fielded a request.
    /// </summary>
    public static byte[] BuildMarkedResponse(byte[] requestBytes, string servedBy) =>
        OkResponse(requestBytes, new { servedBy });
}
