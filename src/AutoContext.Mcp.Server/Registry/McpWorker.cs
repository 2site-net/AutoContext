namespace AutoContext.Mcp.Server.Registry;

using System.Text.RegularExpressions;

using AutoContext.Mcp.Server.Workers.Transport;

/// <summary>
/// One worker entry inside the registry — the worker's short identifier,
/// full project name, and the MCP Tool definitions it exposes. The
/// transport <see cref="Endpoint"/> is derived from <see cref="Id"/>.
/// </summary>
public sealed partial record McpWorker
{
    private const string IdPatternText = "^[a-z][a-z0-9-]*$";

    /// <summary>
    /// Unique worker identifier (e.g. <c>"dotnet"</c>). Kebab-case, lowercase —
    /// must match <c>^[a-z][a-z0-9-]*$</c> (same pattern the registry JSON
    /// Schema enforces).
    /// </summary>
    public required string Id
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            if (!IdPattern().IsMatch(value))
            {
                throw new ArgumentException(
                    $"Worker id '{value}' is not lower kebab-case (must match '{IdPatternText}').",
                    nameof(value));
            }

            field = value;
        }
    } = string.Empty;

    /// <summary>Full project name (e.g. <c>"AutoContext.Worker.DotNet"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Transport-agnostic endpoint identifier (today: a named pipe name).</summary>
    public string Endpoint => EndpointFormatter.Format(Id);

    /// <summary>MCP Tool definitions exposed by this worker.</summary>
    public required IReadOnlyList<McpToolDefinition> Tools { get; init; }

    [GeneratedRegex(IdPatternText)]
    private static partial Regex IdPattern();
}
