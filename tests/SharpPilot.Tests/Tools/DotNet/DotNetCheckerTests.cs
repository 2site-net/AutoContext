namespace SharpPilot.Tests.Tools.DotNet;

using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging.Abstractions;

using SharpPilot.Tools.Checkers.DotNet;

[Collection("ToolsStatus")]
public sealed class DotNetCheckerTests
{
    [Fact]
    public void Should_pass_when_all_checks_pass()
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
        var result = new DotNetChecker(NullLogger<DotNetChecker>.Instance).Check(source, new JsonObject { ["productionFileName"] = "MyClass.cs" });

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_report_violations_from_multiple_checkers()
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
        var result = new DotNetChecker(NullLogger<DotNetChecker>.Instance).Check(source, new JsonObject { ["productionFileName"] = "MyClass.cs" });

        // Assert
        Assert.StartsWith("❌", result);
        Assert.Contains("Block-scoped namespace", result);
        Assert.Contains("#region", result);
    }

    [Fact]
    public void Should_aggregate_only_failing_checks()
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
        var result = new DotNetChecker(NullLogger<DotNetChecker>.Instance).Check(source);

        // Assert — should contain naming violation but not a success message for passing checks
        Assert.StartsWith("❌", result);
        Assert.DoesNotContain("✅", result);
    }

    [Fact]
    public void Should_throw_on_null_or_whitespace_source()
    {
        Assert.Throws<ArgumentException>(() => new DotNetChecker(NullLogger<DotNetChecker>.Instance).Check(""));
        Assert.Throws<ArgumentException>(() => new DotNetChecker(NullLogger<DotNetChecker>.Instance).Check("   "));
    }
}
