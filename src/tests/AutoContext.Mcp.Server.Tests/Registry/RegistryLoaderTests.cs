namespace AutoContext.Mcp.Server.Tests.Registry;

using System.IO;
using System.Text.Json;

using AutoContext.Mcp.Server.Registry;

public sealed class RegistryLoaderTests
{
    [Fact]
    public void Should_parse_real_registry()
    {
        // Act
        var registry = RegistryLoader.Parse(RegistryEmbeddedResourceLoader.Json);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("1", registry.SchemaVersion),
            () => Assert.NotEmpty(registry.Workers),
            () => Assert.Contains(registry.Workers, w => w.Name == "AutoContext.Worker.DotNet"),
            () => Assert.Contains(registry.Workers, w => w.Name == "AutoContext.Worker.Workspace"),
            () => Assert.Contains(registry.Workers, w => w.Name == "AutoContext.Worker.Web"));
    }

    [Fact]
    public void Should_expose_typed_tool_definitions()
    {
        // Act
        var registry = RegistryLoader.Parse(RegistryEmbeddedResourceLoader.Json);
        var dotnet = registry.Workers.Single(w => w.Name == "AutoContext.Worker.DotNet");
        var csharp = dotnet.Tools.Single(t => t.Name == "analyze_csharp_code");

        // Assert
        Assert.Multiple(
            () => Assert.Equal("autocontext.worker-dotnet", dotnet.Endpoint),
            () => Assert.True(csharp.Parameters["content"].Required),
            () => Assert.False(csharp.Parameters["originalPath"].Required),
            () => Assert.Contains(csharp.Tasks, t => t.Name == "analyze_csharp_coding_style"));
    }

    [Fact]
    public void Should_default_editorconfig_to_empty_when_omitted()
    {
        // Arrange
        const string json = """
            {
              "schemaVersion": "1",
              "workers": [{
                "name": "AutoContext.Worker.Workspace",
                "endpoint": "autocontext.worker-workspace",
                "tools": [{
                  "name": "read_editorconfig_properties",
                  "description": "x",
                  "parameters": { "path": { "type": "string", "description": "x", "required": true } },
                  "tasks": [{ "name": "get_editorconfig_rules" }]
                }]
              }]
            }
            """;

        // Act
        var registry = RegistryLoader.Parse(json);
        var task = registry.Workers[0].Tools[0].Tasks[0];

        // Assert
        Assert.Multiple(
            () => Assert.Equal("get_editorconfig_rules", task.Name),
            () => Assert.Empty(task.EditorConfig));
    }

    [Fact]
    public async Task LoadAsync_should_read_from_disk()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"registry-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, RegistryEmbeddedResourceLoader.Json, TestContext.Current.CancellationToken);

        try
        {
            // Act
            var registry = await RegistryLoader.LoadAsync(path, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal("1", registry.SchemaVersion);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Should_throw_JsonException_when_schemaVersion_missing()
    {
        Assert.Throws<JsonException>(() => RegistryLoader.Parse("""{ "workers": [] }"""));
    }

    [Fact]
    public void Should_throw_JsonException_when_workers_missing()
    {
        Assert.Throws<JsonException>(() => RegistryLoader.Parse("""{ "schemaVersion": "1" }"""));
    }

    [Fact]
    public void Should_throw_ArgumentNullException_when_json_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => RegistryLoader.Parse(null!));
    }
}
