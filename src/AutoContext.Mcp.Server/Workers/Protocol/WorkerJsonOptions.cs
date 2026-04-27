namespace AutoContext.Mcp.Server.Workers.Protocol;

using System.Text.Json;

/// <summary>
/// Frozen <see cref="JsonSerializerOptions"/> shared by the protocol
/// types in this project. CamelCase keys, no indentation — matches
/// <c>AutoContext.Worker.Hosting.McpToolService</c> on the worker side
/// so both ends of the pipe agree on shape.
/// </summary>
/// <remarks>
/// The protocol record types pin their JSON names with explicit
/// <see cref="System.Text.Json.Serialization.JsonPropertyNameAttribute"/>
/// attributes so callers can serialize them with default options and still
/// get canonical camelCase output.
/// </remarks>
internal static class WorkerJsonOptions
{
    public static JsonSerializerOptions Instance { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
        };

        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
