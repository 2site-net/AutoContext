namespace AutoContext.Engine.Core.Workspace.Config;

using System.Text.Json.Serialization;

/// <summary>
/// A single <c>mcpTools</c> value, which on disk is either an object
/// (<see cref="JsonMcpToolConfigEntry"/>) or the literal <c>false</c>.
/// The bare <c>false</c> means "disabled, with nothing else to
/// record"; the object form additionally carries
/// <see cref="JsonMcpToolConfigEntry.Enabled"/>,
/// <see cref="JsonMcpToolConfigEntry.Version"/>, and
/// <see cref="JsonMcpToolConfigEntry.DisabledTasks"/>. The two forms
/// are kept distinct on disk: the writer only upgrades <c>false</c> to
/// <c>{ enabled: false }</c> when it has extra state to store.
/// </summary>
[JsonConverter(typeof(JsonMcpToolConfigValueConverter))]
internal sealed record JsonMcpToolConfigValue
{
    private JsonMcpToolConfigValue(JsonMcpToolConfigEntry? entry)
    {
        Entry = entry;
    }

    /// <summary>
    /// The shorthand <c>false</c> value: a disabled tool with no
    /// other state.
    /// </summary>
    public static JsonMcpToolConfigValue Disabled { get; } = new(entry: null);

    /// <summary>
    /// The object form, or <see langword="null"/> when this value is
    /// the shorthand <c>false</c>.
    /// </summary>
    public JsonMcpToolConfigEntry? Entry { get; }

    /// <summary>
    /// <see langword="true"/> when this value is the shorthand
    /// <c>false</c> rather than an object.
    /// </summary>
    public bool IsShorthandDisabled
        => Entry is null;

    /// <summary>
    /// Wraps an object-form <paramref name="entry"/>.
    /// </summary>
    /// <param name="entry">The entry to wrap. Must not be
    /// <see langword="null"/>; use <see cref="Disabled"/> for the
    /// shorthand form.</param>
    public static JsonMcpToolConfigValue FromEntry(JsonMcpToolConfigEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new JsonMcpToolConfigValue(entry);
    }
}
