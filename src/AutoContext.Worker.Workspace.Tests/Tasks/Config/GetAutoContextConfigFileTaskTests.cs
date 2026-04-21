namespace AutoContext.Worker.Workspace.Tests.Tasks.Config;

using System.Text.Json;

using AutoContext.Worker.Workspace.Hosting;
using AutoContext.Worker.Workspace.Tasks.Config;
using AutoContext.Worker.Workspace.Tests._Utils;

using Microsoft.Extensions.Options;

public sealed class GetAutoContextConfigFileTaskTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task Should_return_empty_mcpTools_when_config_missing()
    {
        // Arrange
        var workspace = _temp.CreateDirectory();
        var sut = CreateSut(workspace);

        // Act
        var output = await McpTaskRunner.RunAsync(sut, new { });

        // Assert
        Assert.Multiple(
            () => Assert.Equal(JsonValueKind.Object, output.GetProperty("mcpTools").ValueKind),
            () => Assert.Empty(output.GetProperty("mcpTools").EnumerateObject()));
    }

    [Fact]
    public async Task Should_expand_false_shorthand_to_disabled_with_empty_disabledTasks()
    {
        // Arrange
        var workspace = _temp.CreateDirectory();
        WriteConfig(workspace, """
            { "mcpTools": { "analyze_csharp_code": false } }
            """);
        var sut = CreateSut(workspace);

        // Act
        var output = await McpTaskRunner.RunAsync(sut, new { });
        var entry = output.GetProperty("mcpTools").GetProperty("analyze_csharp_code");

        // Assert
        Assert.Multiple(
            () => Assert.False(entry.GetProperty("enabled").GetBoolean()),
            () => Assert.Empty(entry.GetProperty("disabledTasks").EnumerateArray()));
    }

    [Fact]
    public async Task Should_expand_true_shorthand_to_enabled_with_empty_disabledTasks()
    {
        // Arrange
        var workspace = _temp.CreateDirectory();
        WriteConfig(workspace, """
            { "mcpTools": { "analyze_csharp_code": true } }
            """);
        var sut = CreateSut(workspace);

        // Act
        var output = await McpTaskRunner.RunAsync(sut, new { });
        var entry = output.GetProperty("mcpTools").GetProperty("analyze_csharp_code");

        // Assert
        Assert.Multiple(
            () => Assert.True(entry.GetProperty("enabled").GetBoolean()),
            () => Assert.Empty(entry.GetProperty("disabledTasks").EnumerateArray()));
    }

    [Fact]
    public async Task Should_preserve_long_form_with_version_and_disabledTasks()
    {
        // Arrange
        var workspace = _temp.CreateDirectory();
        WriteConfig(workspace, """
            {
              "mcpTools": {
                "analyze_nuget_references": {
                  "enabled": true,
                  "version": "1.0.0",
                  "disabledTasks": ["check_nuget_hygiene"]
                }
              }
            }
            """);
        var sut = CreateSut(workspace);

        // Act
        var output = await McpTaskRunner.RunAsync(sut, new { });
        var entry = output.GetProperty("mcpTools").GetProperty("analyze_nuget_references");
        var disabled = entry.GetProperty("disabledTasks").EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty).ToArray();

        // Assert
        Assert.Multiple(
            () => Assert.True(entry.GetProperty("enabled").GetBoolean()),
            () => Assert.Equal("1.0.0", entry.GetProperty("version").GetString()),
            () => Assert.Equal(["check_nuget_hygiene"], disabled));
    }

    [Fact]
    public async Task Should_accept_legacy_kebab_keys_on_input()
    {
        // Arrange
        var workspace = _temp.CreateDirectory();
        WriteConfig(workspace, """
            {
              "mcp-tools": {
                "analyze_nuget_references": {
                  "enabled": true,
                  "disabled-features": ["check_nuget_hygiene"]
                }
              }
            }
            """);
        var sut = CreateSut(workspace);

        // Act
        var output = await McpTaskRunner.RunAsync(sut, new { });
        var entry = output.GetProperty("mcpTools").GetProperty("analyze_nuget_references");
        var disabledLegacy = entry.GetProperty("disabledTasks").EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty).ToArray();

        // Assert
        Assert.Multiple(
            () => Assert.True(entry.GetProperty("enabled").GetBoolean()),
            () => Assert.Equal(["check_nuget_hygiene"], disabledLegacy));
    }

    [Fact]
    public async Task Should_scope_disabledTasks_per_parent()
    {
        // Arrange
        var workspace = _temp.CreateDirectory();
        WriteConfig(workspace, """
            {
              "mcpTools": {
                "analyze_csharp_code": {
                  "enabled": true,
                  "disabledTasks": ["check_csharp_coding_style"]
                },
                "analyze_nuget_references": {
                  "enabled": true,
                  "disabledTasks": []
                }
              }
            }
            """);
        var sut = CreateSut(workspace);

        // Act
        var output = await McpTaskRunner.RunAsync(sut, new { });
        var tools = output.GetProperty("mcpTools");
        var csDisabled = tools.GetProperty("analyze_csharp_code")
            .GetProperty("disabledTasks").EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty).ToArray();
        var nugetDisabled = tools.GetProperty("analyze_nuget_references")
            .GetProperty("disabledTasks").EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty).ToArray();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["check_csharp_coding_style"], csDisabled),
            () => Assert.Empty(nugetDisabled));
    }

    private static GetAutoContextConfigFileTask CreateSut(string workspaceRoot)
    {
        var options = Options.Create(new WorkerOptions { WorkspaceRoot = workspaceRoot });

        return new GetAutoContextConfigFileTask(options);
    }

    private static void WriteConfig(string workspaceRoot, string json)
    {
        File.WriteAllText(Path.Combine(workspaceRoot, ".autocontext.json"), json);
    }
}
namespace AutoContext.Worker.Workspace.Tests.Tasks.Config;

using System.Text.Json;

using AutoContext.Worker.Workspace.Hosting;
using AutoContext.Worker.Workspace.Tasks.Config;
using AutoContext.Worker.Workspace.Tests._Utils;

using Microsoft.Extensions.Options;

public sealed class GetAutoContextConfigFileTaskTests : IDisposable
{
    private readonly TempDirectory _workspace = new("ac-cfg-tests");

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task Should_return_empty_mcpTools_when_config_missing()
    {
        // Act
        var output = await ExecuteAsync();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(JsonValueKind.Object, output.GetProperty("mcpTools").ValueKind),
            () => Assert.Empty(output.GetProperty("mcpTools").EnumerateObject()));
    }

    [Fact]
    public async Task Should_expand_false_shorthand_to_disabled_with_empty_disabledTasks()
    {
        // Arrange
        _workspace.WriteFile(".autocontext.json", """
            { "mcpTools": { "analyze_csharp_code": false } }
            """);

        // Act
        var output = await ExecuteAsync();
        var entry = output.GetProperty("mcpTools").GetProperty("analyze_csharp_code");

        // Assert
        Assert.Multiple(
            () => Assert.False(entry.GetProperty("enabled").GetBoolean()),
            () => Assert.Empty(entry.GetProperty("disabledTasks").EnumerateArray()));
    }

    [Fact]
    public async Task Should_expand_true_shorthand_to_enabled_with_empty_disabledTasks()
    {
        // Arrange
        _workspace.WriteFile(".autocontext.json", """
            { "mcpTools": { "analyze_csharp_code": true } }
            """);

        // Act
        var output = await ExecuteAsync();
        var entry = output.GetProperty("mcpTools").GetProperty("analyze_csharp_code");

        // Assert
        Assert.Multiple(
            () => Assert.True(entry.GetProperty("enabled").GetBoolean()),
            () => Assert.Empty(entry.GetProperty("disabledTasks").EnumerateArray()));
    }

    [Fact]
    public async Task Should_preserve_long_form_with_version_and_disabledTasks()
    {
        // Arrange
        _workspace.WriteFile(".autocontext.json", """
            {
              "mcpTools": {
                "analyze_nuget_references": {
                  "enabled": true,
                  "version": "1.0.0",
                  "disabledTasks": ["check_nuget_hygiene"]
                }
              }
            }
            """);

        // Act
        var output = await ExecuteAsync();
        var entry = output.GetProperty("mcpTools").GetProperty("analyze_nuget_references");
        var disabled = entry.GetProperty("disabledTasks").EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty).ToArray();

        // Assert
        Assert.Multiple(
            () => Assert.True(entry.GetProperty("enabled").GetBoolean()),
            () => Assert.Equal("1.0.0", entry.GetProperty("version").GetString()),
            () => Assert.Equal(["check_nuget_hygiene"], disabled));
    }

    [Fact]
    public async Task Should_accept_legacy_kebab_keys_on_input()
    {
        // Arrange
        _workspace.WriteFile(".autocontext.json", """
            {
              "mcp-tools": {
                "analyze_nuget_references": {
                  "enabled": true,
                  "disabled-features": ["check_nuget_hygiene"]
                }
              }
            }
            """);

        // Act
        var output = await ExecuteAsync();
        var entry = output.GetProperty("mcpTools").GetProperty("analyze_nuget_references");
        var disabledLegacy = entry.GetProperty("disabledTasks").EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty).ToArray();

        // Assert
        Assert.Multiple(
            () => Assert.True(entry.GetProperty("enabled").GetBoolean()),
            () => Assert.Equal(["check_nuget_hygiene"], disabledLegacy));
    }

    [Fact]
    public async Task Should_scope_disabledTasks_per_parent()
    {
        // Arrange
        _workspace.WriteFile(".autocontext.json", """
            {
              "mcpTools": {
                "analyze_csharp_code": {
                  "enabled": true,
                  "disabledTasks": ["check_csharp_coding_style"]
                },
                "analyze_nuget_references": {
                  "enabled": true,
                  "disabledTasks": []
                }
              }
            }
            """);

        // Act
        var output = await ExecuteAsync();
        var tools = output.GetProperty("mcpTools");
        var csDisabled = tools.GetProperty("analyze_csharp_code")
            .GetProperty("disabledTasks").EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty).ToArray();
        var nugetDisabled = tools.GetProperty("analyze_nuget_references")
            .GetProperty("disabledTasks").EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty).ToArray();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["check_csharp_coding_style"], csDisabled),
            () => Assert.Empty(nugetDisabled));
    }

    private Task<JsonElement> ExecuteAsync()
    {
        var options = Options.Create(new WorkerOptions { WorkspaceRoot = _workspace.Path });

        return McpTaskRunner.RunAsync(new GetAutoContextConfigFileTask(options), new { });
    }
}
namespace AutoContext.Worker.Workspace.Tests.Tasks.Config;

using System.Text.Json;

using AutoContext.Worker.Workspace.Hosting;
using AutoContext.Worker.Workspace.Tasks.Config;

using Microsoft.Extensions.Options;

public sealed class GetAutoContextConfigFileTaskTests : IDisposable
{
    private readonly string _tempDir;

    public GetAutoContextConfigFileTaskTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ac-cfg-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Should_return_empty_mcpTools_when_config_missing()
    {
        // Act
        var output = await ExecuteAsync();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(JsonValueKind.Object, output.GetProperty("mcpTools").ValueKind),
            () => Assert.Empty(output.GetProperty("mcpTools").EnumerateObject()));
    }

    [Fact]
    public async Task Should_expand_false_shorthand_to_disabled_with_empty_disabledTasks()
    {
        // Arrange
        WriteConfig("""
            { "mcpTools": { "analyze_csharp_code": false } }
            """);

        // Act
        var output = await ExecuteAsync();
        var entry = output.GetProperty("mcpTools").GetProperty("analyze_csharp_code");

        // Assert
        Assert.Multiple(
            () => Assert.False(entry.GetProperty("enabled").GetBoolean()),
            () => Assert.Empty(entry.GetProperty("disabledTasks").EnumerateArray()));
    }

    [Fact]
    public async Task Should_expand_true_shorthand_to_enabled_with_empty_disabledTasks()
    {
        // Arrange
        WriteConfig("""
            { "mcpTools": { "analyze_csharp_code": true } }
            """);

        // Act
        var output = await ExecuteAsync();
        var entry = output.GetProperty("mcpTools").GetProperty("analyze_csharp_code");

        // Assert
        Assert.Multiple(
            () => Assert.True(entry.GetProperty("enabled").GetBoolean()),
            () => Assert.Empty(entry.GetProperty("disabledTasks").EnumerateArray()));
    }

    [Fact]
    public async Task Should_preserve_long_form_with_version_and_disabledTasks()
    {
        // Arrange
        WriteConfig("""
            {
              "mcpTools": {
                "analyze_nuget_references": {
                  "enabled": true,
                  "version": "1.0.0",
                  "disabledTasks": ["check_nuget_hygiene"]
                }
              }
            }
            """);

        // Act
        var output = await ExecuteAsync();
        var entry = output.GetProperty("mcpTools").GetProperty("analyze_nuget_references");
        var disabled = entry.GetProperty("disabledTasks").EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty).ToArray();

        // Assert
        Assert.Multiple(
            () => Assert.True(entry.GetProperty("enabled").GetBoolean()),
            () => Assert.Equal("1.0.0", entry.GetProperty("version").GetString()),
            () => Assert.Equal(["check_nuget_hygiene"], disabled));
    }

    [Fact]
    public async Task Should_accept_legacy_kebab_keys_on_input()
    {
        // Arrange
        WriteConfig("""
            {
              "mcp-tools": {
                "analyze_nuget_references": {
                  "enabled": true,
                  "disabled-features": ["check_nuget_hygiene"]
                }
              }
            }
            """);

        // Act
        var output = await ExecuteAsync();
        var entry = output.GetProperty("mcpTools").GetProperty("analyze_nuget_references");
        var disabledLegacy = entry.GetProperty("disabledTasks").EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty).ToArray();

        // Assert
        Assert.Multiple(
            () => Assert.True(entry.GetProperty("enabled").GetBoolean()),
            () => Assert.Equal(["check_nuget_hygiene"], disabledLegacy));
    }

    [Fact]
    public async Task Should_scope_disabledTasks_per_parent()
    {
        // Arrange
        WriteConfig("""
            {
              "mcpTools": {
                "analyze_csharp_code": {
                  "enabled": true,
                  "disabledTasks": ["check_csharp_coding_style"]
                },
                "analyze_nuget_references": {
                  "enabled": true,
                  "disabledTasks": []
                }
              }
            }
            """);

        // Act
        var output = await ExecuteAsync();
        var tools = output.GetProperty("mcpTools");
        var csDisabled = tools.GetProperty("analyze_csharp_code")
            .GetProperty("disabledTasks").EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty).ToArray();
        var nugetDisabled = tools.GetProperty("analyze_nuget_references")
            .GetProperty("disabledTasks").EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty).ToArray();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["check_csharp_coding_style"], csDisabled),
            () => Assert.Empty(nugetDisabled));
    }

    private void WriteConfig(string json)
    {
        File.WriteAllText(Path.Combine(_tempDir, ".autocontext.json"), json);
    }

    private async Task<JsonElement> ExecuteAsync()
    {
        var options = Options.Create(new WorkerOptions { WorkspaceRoot = _tempDir });
        var task = new GetAutoContextConfigFileTask(options);
        var data = JsonSerializer.SerializeToElement(new { });

        return await task.ExecuteAsync(data, TestContext.Current.CancellationToken);
    }
}
