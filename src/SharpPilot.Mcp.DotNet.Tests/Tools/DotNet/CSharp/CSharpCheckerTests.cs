namespace SharpPilot.Mcp.DotNet.Tests.Tools.DotNet.CSharp;

using Microsoft.Extensions.Logging.Abstractions;

using SharpPilot.Mcp.DotNet.Tools.Checkers.DotNet.CSharp;

public sealed class CSharpCheckerTests
{
    [Fact]
    public async Task Should_pass_when_all_checks_pass()
    {
        // Arrange
        var source = """
            namespace MyApp;

            /// <summary>
            /// A well-formed class.
            /// </summary>
            public class MyClass
            {
                private readonly int _value;

                /// <summary>
                /// Initializes a new instance.
                /// </summary>
                public MyClass(int value)
                {
                    _value = value;
                }

                /// <summary>
                /// Gets the value.
                /// </summary>
                public int Value
                    => _value;
            }
            """;

        // Act
        var result = await new CSharpChecker(NullLogger<CSharpChecker>.Instance).CheckAsync(source, productionFileName: "MyClass.cs");

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_report_violations_from_multiple_checkers()
    {
        // Arrange — block-scoped namespace (project structure) + region (code style)
        var source = """
            namespace MyApp
            {
                #region Fields
                public class myclass
                {
                    private int Value;
                }
                #endregion
            }
            """;

        // Act
        var result = await new CSharpChecker(NullLogger<CSharpChecker>.Instance).CheckAsync(source, productionFileName: "MyClass.cs");

        // Assert
        Assert.StartsWith("❌", result);
        Assert.Contains("Block-scoped namespace", result);
        Assert.Contains("#region", result);
    }

    [Fact]
    public async Task Should_aggregate_only_failing_checks()
    {
        // Arrange — passes style but has a naming violation (PascalCase field without underscore)
        var source = """
            namespace MyApp;

            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                private int Value;
            }
            """;

        // Act
        var result = await new CSharpChecker(NullLogger<CSharpChecker>.Instance).CheckAsync(source);

        // Assert — should contain naming violation but not a success message for passing checks
        Assert.StartsWith("❌", result);
        Assert.DoesNotContain("✅", result);
    }

    [Fact]
    public async Task Should_throw_on_null_or_whitespace_source()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => new CSharpChecker(NullLogger<CSharpChecker>.Instance).CheckAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => new CSharpChecker(NullLogger<CSharpChecker>.Instance).CheckAsync("   "));
    }
}
