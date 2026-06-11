namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// The concrete origin a projected or raw instructions body was read
/// from. Carried on <see cref="JsonInstructionsListRow.Source"/> and
/// <see cref="JsonInstructionsGetRawOkResult.Source"/> — both name a
/// single resolved file, never the <c>active</c> selector (that is a
/// request-side concern; see <see cref="InstructionsRawSource"/>).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<InstructionsSource>))]
public enum InstructionsSource
{
    /// <summary>The file shipped inside the engine's bundled corpus.</summary>
    [JsonStringEnumMemberName("bundled")]
    Bundled,

    /// <summary>
    /// A workspace shadow under <c>.github/instructions/</c> that
    /// takes precedence over the bundled file.
    /// </summary>
    [JsonStringEnumMemberName("override")]
    Override,
}
