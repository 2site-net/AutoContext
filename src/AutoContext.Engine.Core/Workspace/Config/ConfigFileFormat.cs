namespace AutoContext.Engine.Core.Workspace.Config;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using AutoContext.Engine.Core.Workspace.Config.Format;

/// <summary>
/// Stateless helpers for the on-disk <c>.autocontext.json</c> format.
/// Centralises the JSON options, the fixed key order, and the parse
/// normalisation so reading and writing always agree on one format:
/// camelCase keys, four-space indentation, and a trailing newline.
/// </summary>
internal static class ConfigFileFormat
{
    /// <summary>
    /// JSON options for the config file: camelCase keys, four-space
    /// indentation, and omission of <see langword="null"/> members so
    /// absent sections never appear on disk.
    /// </summary>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        IndentCharacter = ' ',
        IndentSize = 4,
        NewLine = "\n",
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Serialises <paramref name="config"/> into the UTF-8 bytes
    /// written to disk. The <c>version</c> field is stamped with
    /// <paramref name="engineVersion"/> and written first, followed by
    /// a trailing newline.
    /// </summary>
    /// <param name="config">Config to persist. Must not be
    /// <see langword="null"/> or empty (callers delete the file
    /// instead of writing an empty config).</param>
    /// <param name="engineVersion">Full semver stamped into
    /// <c>version</c>.</param>
    /// <returns>UTF-8 bytes including the trailing newline.</returns>
    public static byte[] Serialize(JsonConfigFile config, string engineVersion)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(engineVersion);

        var ordered = config with { Version = engineVersion };
        var json = JsonSerializer.Serialize(ordered, SerializerOptions);
        return Encoding.UTF8.GetBytes(json + "\n");
    }

    /// <summary>
    /// Tries to parse <paramref name="bytes"/> into a normalised
    /// config. Empty input parses successfully to
    /// <see cref="JsonConfigFile.Empty"/>; malformed JSON
    /// returns <see langword="false"/>.
    /// </summary>
    /// <param name="bytes">Raw file bytes.</param>
    /// <param name="config">The parsed config on success;
    /// <see cref="JsonConfigFile.Empty"/> on failure.</param>
    /// <returns><see langword="true"/> when the bytes parsed
    /// (including the empty-file case); otherwise
    /// <see langword="false"/>.</returns>
    public static bool TryDeserialize(byte[] bytes, out JsonConfigFile config)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        config = JsonConfigFile.Empty;

        if (bytes.Length == 0)
        {
            return true;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<JsonConfigFile>(bytes, SerializerOptions);

            if (parsed is null)
            {
                return false;
            }

            config = Normalize(parsed);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IReadOnlyList<string>? EmptyToNull(IReadOnlyList<string>? list)
        => list is { Count: > 0 } ? list : null;

    /// <summary>
    /// Drops sections and members the writer never emits, so a
    /// hand-edited file (empty arrays, <c>disabled: false</c>, empty
    /// maps) parses to the same canonical form the engine writes.
    /// </summary>
    private static JsonConfigFile Normalize(JsonConfigFile parsed)
        => new()
        {
            Version = parsed.Version,
            Engine = NormalizeEngine(parsed.Engine),
            Diagnostic = parsed.Diagnostic,
            Instructions = NormalizeInstructions(parsed.Instructions),
            McpTools = NormalizeTools(parsed.McpTools),
        };

    private static JsonConfigFileEngine? NormalizeEngine(JsonConfigFileEngine? engine)
    {
        if (engine is null)
        {
            return null;
        }

        var directories = EmptyToNull(engine.InstructionsOverridesRoots);

        return directories is null ? null : engine with { InstructionsOverridesRoots = directories };
    }

    private static Dictionary<string, JsonConfigFileInstructionsEntry>? NormalizeInstructions(
        IReadOnlyDictionary<string, JsonConfigFileInstructionsEntry>? instructions)
    {
        if (instructions is null || instructions.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, JsonConfigFileInstructionsEntry>(instructions.Count);

        foreach (var (fileName, entry) in instructions)
        {
            result[fileName] = entry with
            {
                Disabled = entry.Disabled is true ? true : null,
                DisabledRules = EmptyToNull(entry.DisabledRules),
            };
        }

        return result;
    }

    private static Dictionary<string, JsonConfigFileMcpToolEntry>? NormalizeTools(
        IReadOnlyDictionary<string, JsonConfigFileMcpToolEntry>? tools)
    {
        if (tools is null || tools.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, JsonConfigFileMcpToolEntry>(tools.Count);

        foreach (var (toolName, entry) in tools)
        {
            result[toolName] = entry with
            {
                Disabled = entry.Disabled is true ? true : null,
                DisabledTasks = EmptyToNull(entry.DisabledTasks),
            };
        }

        return result;
    }
}
