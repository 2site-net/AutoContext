namespace AutoContext.Workers.Manifest.Generator.Tests.Support;

using AutoContext.Workers.Manifest.Generator;

internal static class WorkersManifestFakeData
{
    internal static JsonWorkerEntry CreateEntry(
        string id = "dotnet",
        string type = "executable",
        string? label = null,
        string command = "${root}/AutoContext.Worker.DotNet")
        => new(id, type, label, command);

    internal static JsonWorkersManifest CreateManifest(params JsonWorkerEntry[] workers)
        => new(workers);

    internal static string Descriptor(string id, string type, string command, string? label = null)
        => label is null
            ? $$"""
                {
                  "id": "{{id}}",
                  "type": "{{type}}",
                  "command": "{{command}}"
                }
                """
            : $$"""
                {
                  "id": "{{id}}",
                  "type": "{{type}}",
                  "label": "{{label}}",
                  "command": "{{command}}"
                }
                """;
}
