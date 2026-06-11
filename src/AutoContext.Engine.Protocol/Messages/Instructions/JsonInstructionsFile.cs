namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// One file's projected body plus section index — the bulk-read shape
/// carried by <see cref="JsonInstructionsFilesResult"/> for
/// <see cref="InstructionsMethods.GetAll"/> and
/// <see cref="InstructionsMethods.GetAlwaysAttached"/>. The body is
/// projected (disabled rules filtered, <c>[INSTxxxx]</c> tags
/// stripped, override preferred over bundled), matching what
/// <see cref="InstructionsMethods.Get"/> returns for one file.
/// </summary>
public sealed record JsonInstructionsFile
{
    /// <summary>The corpus file name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>File basename (the stable key).</summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    /// <summary>The corpus file name (<c>&lt;key&gt;.instructions.md</c>).</summary>
    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }

    /// <summary>The projected body.</summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    /// <summary>Section index for the projected body, in document order.</summary>
    [JsonPropertyName("sections")]
    public IReadOnlyList<JsonInstructionsSection> Sections { get; init; } = [];
}
