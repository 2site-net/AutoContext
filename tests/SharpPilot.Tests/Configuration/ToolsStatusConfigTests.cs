namespace SharpPilot.Tests.Configuration;

using SharpPilot.Configuration;

[Collection("ToolsStatus")]
public sealed class ToolsStatusConfigTests : IDisposable
{
    private readonly string _workspacePath = Path.Combine(Path.GetTempPath(), $"sharppilot-test-{Guid.NewGuid():N}");
    private readonly string _configFilePath;

    public ToolsStatusConfigTests()
    {
        Directory.CreateDirectory(_workspacePath);
        _configFilePath = Path.Combine(_workspacePath, ".sharppilot.json");
        ToolsStatusConfig.Configure(_workspacePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspacePath))
        {
            Directory.Delete(_workspacePath, recursive: true);
        }
    }

    [Fact]
    public void Should_return_true_when_config_file_is_missing()
    {
        // Act
        var result = ToolsStatusConfig.IsEnabled("check_csharp_coding_style");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Should_return_true_when_tool_is_not_disabled()
    {
        // Arrange
        File.WriteAllText(_configFilePath, """{ "tools": { "disabledTools": ["check_csharp_async_patterns"] } }""");

        // Act
        var result = ToolsStatusConfig.IsEnabled("check_csharp_coding_style");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Should_return_false_when_tool_is_disabled()
    {
        // Arrange
        File.WriteAllText(_configFilePath, """{ "tools": { "disabledTools": ["check_csharp_coding_style"] } }""");

        // Act
        var result = ToolsStatusConfig.IsEnabled("check_csharp_coding_style");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Should_return_true_when_tools_section_is_absent()
    {
        // Arrange
        File.WriteAllText(_configFilePath, """{ "version": "0.5.0" }""");

        // Act
        var result = ToolsStatusConfig.IsEnabled("check_csharp_coding_style");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Should_return_true_when_file_contains_invalid_json()
    {
        // Arrange
        File.WriteAllText(_configFilePath, "not valid json!!!");

        // Act
        var result = ToolsStatusConfig.IsEnabled("check_csharp_coding_style");

        // Assert
        Assert.True(result);
    }
}
