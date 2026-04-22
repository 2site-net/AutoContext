namespace AutoContext.Mcp.Tools.Tests.Manifest;

using System.IO;
using System.Text.Json;

using AutoContext.Mcp.Tools.Manifest;

public sealed class ManifestLoaderTests
{
    [Fact]
    public void Should_parse_real_manifest()
    {
        // Act
        var manifest = ManifestLoader.Parse(RealManifestFixture.Json);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("1", manifest.SchemaVersion),
            () => Assert.Contains("dotnet", manifest.Workers.Keys),
            () => Assert.Contains("workspace", manifest.Workers.Keys),
            () => Assert.Contains("web", manifest.Workers.Keys));
    }

    [Fact]
    public void Should_expose_typed_tool_definitions()
    {
        // Act
        var manifest = ManifestLoader.Parse(RealManifestFixture.Json);
        var dotnet = manifest.Workers["dotnet"][0];
        var csharp = dotnet.Tools.First(t => t.Definition.Name == "analyze_csharp_code");

        // Assert
        Assert.Multiple(
            () => Assert.Equal("autocontext.dotnet-worker", dotnet.Endpoint),
            () => Assert.True(csharp.Definition.Parameters["content"].Required),
            () => Assert.False(csharp.Definition.Parameters["originalPath"].Required),
            () => Assert.Contains("hasCSharp", csharp.WorkspaceFlags));
    }

    [Fact]
    public void Should_default_priority_to_zero_when_omitted()
    {
        // Arrange
        const string json = """
            {
              "schemaVersion": "1",
              "workspace": [{
                "tag": "Workspace", "description": "x",
                "endpoint": "autocontext.workspace-worker",
                "tools": [{
                  "tag": "EditorConfig", "description": "x",
                  "definition": {
                    "name": "read_editorconfig_properties",
                    "description": "x",
                    "parameters": { "path": { "type": "string", "description": "x", "required": true } }
                  },
                  "tasks": ["get_editorconfig_rules"]
                }],
                "tasks": [
                  { "name": "get_editorconfig_rules", "version": "1.0.0" }
                ]
              }]
            }
            """;

        // Act
        var manifest = ManifestLoader.Parse(json);
        var task = manifest.Workers["workspace"][0].Tasks[0];

        // Assert
        Assert.Multiple(
            () => Assert.Equal(0, task.Priority),
            () => Assert.Empty(task.EditorConfig),
            () => Assert.Null(task.Description));
    }

    [Fact]
    public async Task LoadAsync_should_read_from_disk()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"manifest-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, RealManifestFixture.Json, TestContext.Current.CancellationToken);

        try
        {
            // Act
            var manifest = await ManifestLoader.LoadAsync(path, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal("1", manifest.SchemaVersion);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Should_throw_JsonException_when_root_is_not_object()
    {
        Assert.Throws<JsonException>(() => ManifestLoader.Parse("[]"));
    }

    [Fact]
    public void Should_throw_JsonException_when_schemaVersion_missing()
    {
        Assert.Throws<JsonException>(() => ManifestLoader.Parse("""{ "dotnet": [] }"""));
    }

    [Fact]
    public void Should_throw_JsonException_when_worker_value_is_not_array()
    {
        Assert.Throws<JsonException>(() => ManifestLoader.Parse(
            """{ "schemaVersion": "1", "dotnet": {} }"""));
    }

    [Fact]
    public void Should_throw_ArgumentNullException_when_json_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => ManifestLoader.Parse(null!));
    }
}
