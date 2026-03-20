namespace SharpPilot.Tests;

using SharpPilot.Configuration;

[Collection("ToolsStatus")]
public sealed class ToolsStatusConfigTests : IDisposable
{
    private static readonly string StatusFilePath =
        Path.Combine(AppContext.BaseDirectory, "tools-status.json");

    public ToolsStatusConfigTests()
    {
        // Ensure clean state before each test.
        if (File.Exists(StatusFilePath))
        {
            File.Delete(StatusFilePath);
        }
    }

    public void Dispose()
    {
        if (File.Exists(StatusFilePath))
        {
            File.Delete(StatusFilePath);
        }
    }

    [Fact]
    public void Should_return_true_when_status_file_is_missing()
    {
        // Act
        var result = ToolsStatusConfig.IsEnabled("check_csharp_coding_style");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Should_return_true_when_tool_is_enabled()
    {
        // Arrange
        File.WriteAllText(StatusFilePath, """{ "check_csharp_coding_style": true }""");

        // Act
        var result = ToolsStatusConfig.IsEnabled("check_csharp_coding_style");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Should_return_false_when_tool_is_disabled()
    {
        // Arrange
        File.WriteAllText(StatusFilePath, """{ "check_csharp_coding_style": false }""");

        // Act
        var result = ToolsStatusConfig.IsEnabled("check_csharp_coding_style");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Should_return_true_when_tool_is_not_in_file()
    {
        // Arrange
        File.WriteAllText(StatusFilePath, """{ "check_csharp_async_patterns": false }""");

        // Act
        var result = ToolsStatusConfig.IsEnabled("check_csharp_coding_style");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Should_return_true_when_file_contains_invalid_json()
    {
        // Arrange
        File.WriteAllText(StatusFilePath, "not valid json!!!");

        // Act
        var result = ToolsStatusConfig.IsEnabled("check_csharp_coding_style");

        // Assert
        Assert.True(result);
    }
}
