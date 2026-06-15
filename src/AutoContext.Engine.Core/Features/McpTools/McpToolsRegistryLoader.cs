namespace AutoContext.Engine.Core.Features.McpTools;

using System.Text.Json;

using AutoContext.Engine.Core.Features.McpTools.Format;
using AutoContext.Engine.Core.Features.McpTools.Snapshot;

/// <summary>
/// Loads the bundled MCP-tools registry into an immutable
/// <see cref="McpToolsRegistry"/> by reading the side-cars shipped
/// beside the engine binary: <c>mcp-tools-registry.json</c> (the
/// hand-authored execution registry) and its
/// <c>mcp-tools-registry.schema.json</c> (the validation contract), plus
/// <c>mcp-tools-catalog.json</c> and
/// <c>mcp-tools-catalog.schema.json</c> to ensure the UI catalog side is
/// also package-valid at startup.
/// </summary>
/// <remarks>
/// The side-cars are engine build artifacts, not user input: a missing
/// file, malformed JSON, a registry that fails schema or cross-reference
/// validation, or a row missing a required field is a packaging defect, so
/// the loader throws rather than degrading to a partial registry. Failing
/// here fails engine startup loudly, which is the intended behaviour. The
/// registry is validated against the schema <em>before</em> it is mapped,
/// so the field-presence guards in the mapping are belt-and-braces.
/// </remarks>
internal static class McpToolsRegistryLoader
{
    /// <summary>The hand-authored execution-registry side-car file name.</summary>
    public const string RegistryFileName = "mcp-tools-registry.json";

    /// <summary>The registry's JSON Schema side-car file name.</summary>
    public const string SchemaFileName = "mcp-tools-registry.schema.json";

    /// <summary>The hand-authored MCP-tools catalog side-car file name.</summary>
    public const string CatalogFileName = "mcp-tools-catalog.json";

    /// <summary>The catalog's JSON Schema side-car file name.</summary>
    public const string CatalogSchemaFileName = "mcp-tools-catalog.schema.json";

    /// <summary>
    /// Reads and validates the registry side-car in
    /// <paramref name="resourcesDirectory"/> and maps it to an immutable
    /// snapshot. The catalog side-cars are also read and schema-validated so
    /// startup fails fast when either half of the MCP-tools manifest set is
    /// malformed.
    /// </summary>
    /// <param name="resourcesDirectory">Absolute path of the directory
    /// holding both side-cars. Must not be <see langword="null"/> or
    /// whitespace.</param>
    /// <param name="cancellationToken">Cancels the file reads.</param>
    /// <returns>The loaded, immutable registry snapshot.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="resourcesDirectory"/> is <see langword="null"/>,
    /// empty, or whitespace.</exception>
    /// <exception cref="FileNotFoundException">A side-car is
    /// missing.</exception>
    /// <exception cref="InvalidOperationException">The registry is
    /// malformed, fails validation, or a row is missing a required
    /// field.</exception>
    public static async Task<McpToolsRegistry> LoadAsync(
        string resourcesDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourcesDirectory);

        var registryPath = Path.Combine(resourcesDirectory, RegistryFileName);
        var schemaPath = Path.Combine(resourcesDirectory, SchemaFileName);
        var catalogPath = Path.Combine(resourcesDirectory, CatalogFileName);
        var catalogSchemaPath = Path.Combine(resourcesDirectory, CatalogSchemaFileName);

        var registryJson = await ReadTextAsync(registryPath, cancellationToken).ConfigureAwait(false);
        var schemaJson = await ReadTextAsync(schemaPath, cancellationToken).ConfigureAwait(false);
        var catalogJson = await ReadTextAsync(catalogPath, cancellationToken).ConfigureAwait(false);
        var catalogSchemaJson = await ReadTextAsync(catalogSchemaPath, cancellationToken)
            .ConfigureAwait(false);

        var validation = McpToolsRegistrySchemaValidator.Validate(registryJson, schemaJson);

        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"Bundled MCP-tools registry '{registryPath}' failed validation:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, validation.Errors));
        }

        var catalogValidation = McpToolsCatalogSchemaValidator.Validate(catalogJson, catalogSchemaJson);

        if (!catalogValidation.IsValid)
        {
            throw new InvalidOperationException(
                $"Bundled MCP-tools catalog '{catalogPath}' failed validation:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, catalogValidation.Errors));
        }

        JsonMcpToolsRegistry? registry;

        try
        {
            registry = JsonSerializer.Deserialize(
                registryJson, McpToolsRegistryJsonContext.Default.JsonMcpToolsRegistry);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Bundled MCP-tools registry '{registryPath}' is not valid JSON.", exception);
        }

        if (registry is null)
        {
            throw Malformed(registryPath, "it deserialised to null.");
        }

        return Map(registry, registryPath);
    }

    private static async Task<string> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Bundled MCP-tools side-car not found: '{path}'.", path);
        }

        return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private static McpToolsRegistry Map(JsonMcpToolsRegistry registry, string registryPath)
    {
        var rows = registry.Tools
            ?? throw Malformed(registryPath, "the 'tools' array is missing.");

        var tools = new List<McpToolsRegistryTool>(rows.Count);

        foreach (var row in rows)
        {
            tools.Add(MapTool(row, registryPath));
        }

        return new McpToolsRegistry(tools);
    }

    private static McpToolsRegistryTool MapTool(JsonMcpToolsRegistryTool tool, string registryPath)
    {
        var name = Required(tool.Name, "tools[].name", registryPath);

        var parameterRows = tool.Parameters
            ?? throw Malformed(registryPath, $"tool '{name}' has no 'parameters'.");

        var parameters = new List<McpToolsRegistryParameter>(parameterRows.Count);

        foreach (var (parameterName, spec) in parameterRows)
        {
            parameters.Add(new McpToolsRegistryParameter
            {
                Name = parameterName,
                Type = Required(spec.Type, $"tool '{name}' parameter '{parameterName}' type", registryPath),
                Description = Required(
                    spec.Description, $"tool '{name}' parameter '{parameterName}' description", registryPath),
                Required = spec.Required ?? false,
            });
        }

        return new McpToolsRegistryTool
        {
            Name = name,
            WorkerId = Required(tool.WorkerId, $"tool '{name}' workerId", registryPath),
            Description = Required(tool.Description, $"tool '{name}' description", registryPath),
            Parameters = parameters,
            Editorconfig = tool.Editorconfig ?? [],
        };
    }

    private static string Required(string? value, string field, string registryPath)
        => string.IsNullOrEmpty(value)
            ? throw Malformed(registryPath, $"required field '{field}' is missing or empty.")
            : value;

    private static InvalidOperationException Malformed(string registryPath, string detail)
        => new($"Bundled MCP-tools registry '{registryPath}' is malformed: {detail}");
}
