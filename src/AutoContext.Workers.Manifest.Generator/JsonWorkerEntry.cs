namespace AutoContext.Workers.Manifest.Generator;

using System.Text.Json.Serialization;

/// <summary>
/// One worker row shared by the hand-authored per-worker
/// <c>.autocontext-worker.json</c> descriptor (read by the generator) and the
/// build-generated <c>workers.json</c> manifest (written by the generator). The
/// generator copies these fields verbatim: <see cref="Command"/> keeps its
/// literal <c>${root}</c> placeholder and any launcher token, and
/// <see cref="Type"/> is informational only — the engine's worker resolver
/// derives how to launch from <see cref="Command"/> at runtime.
/// </summary>
internal sealed class JsonWorkerEntry(string id, string type, string? label, string command)
{
    /// <summary>Gets the launch command, with the literal <c>${root}</c> placeholder intact.</summary>
    [JsonPropertyOrder(3)]
    public string Command { get; } = command;

    /// <summary>Gets the worker id.</summary>
    [JsonPropertyOrder(0)]
    public string Id { get; } = id;

    /// <summary>Gets the optional human-readable label, or <see langword="null"/>.</summary>
    [JsonPropertyOrder(2)]
    public string? Label { get; } = label;

    /// <summary>Gets the informational worker kind (<c>executable</c>, <c>script</c>, or <c>library</c>).</summary>
    [JsonPropertyOrder(1)]
    public string Type { get; } = type;
}
