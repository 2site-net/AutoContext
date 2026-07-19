namespace AutoContext.Engine.Tests;

using AutoContext.Engine;
using AutoContext.Engine.Tests.Support;

/// <summary>
/// End-to-end smoke tests for <see cref="Program.Main"/> — exercises
/// the wiring between the <c>System.CommandLine</c> parse pipeline,
/// the <c>autocontext-engine: </c> stderr prefix, and the built-in
/// <c>--version</c> action. The daemon and MCP-server happy paths are
/// not covered here because the host blocks on shutdown until stdio EOF
/// or a lifecycle clamp fires; <see cref="EngineCommandTests"/> already
/// covers the parser-and-build half end-to-end at unit speed, and the
/// stdio MCP-server role is exercised out-of-process by its smoke test.
/// </summary>
[Collection(ConsoleRedirection.Name)]
public sealed class ProgramTests
{
    [Fact]
    public async Task Should_print_version_to_stdout_and_exit_zero()
    {
        // Arrange
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        Console.SetOut(stdout);
        Console.SetError(stderr);

        try
        {
            // Act
            var exitCode = await Program.Main(["--version"]);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(0, exitCode),
                () => Assert.False(string.IsNullOrWhiteSpace(stdout.ToString())),
                () => Assert.Equal(string.Empty, stderr.ToString()));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task Should_write_parser_errors_with_prefix_to_stderr_and_exit_non_zero()
    {
        // Arrange
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        Console.SetOut(stdout);
        Console.SetError(stderr);

        try
        {
            // Act
            var exitCode = await Program.Main(["--bogus"]);

            // Assert
            Assert.Multiple(
                () => Assert.NotEqual(0, exitCode),
                () => Assert.Equal(string.Empty, stdout.ToString()),
                () => Assert.Contains("autocontext-engine:", stderr.ToString(), StringComparison.Ordinal));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task Should_write_role_errors_with_prefix_to_stderr_and_exit_non_zero()
    {
        // Arrange
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        Console.SetOut(stdout);
        Console.SetError(stderr);

        try
        {
            // Act — daemon argv missing --instance-id passes the parser
            // but fails the cross-option role check.
            var exitCode = await Program.Main(["--workspace", EngineCommandArgsFakeData.GetWorkspacePathArgValue()]);

            // Assert
            Assert.Multiple(
                () => Assert.NotEqual(0, exitCode),
                () => Assert.Equal(string.Empty, stdout.ToString()),
                () => Assert.Contains("autocontext-engine:", stderr.ToString(), StringComparison.Ordinal),
                () => Assert.Contains("--instance-id", stderr.ToString(), StringComparison.Ordinal));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
