namespace SharpPilot.Mcp.DotNet.Tests.Tools.DotNet.CSharp;

using SharpPilot.Mcp.DotNet.Tools.Checkers.DotNet.CSharp;

public sealed class CSharpNullableContextCheckerTests
{
    [Fact]
    public async Task Should_pass_code_without_nullable_violations()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public string? Name { get; set; }

                public void SetName(string? name)
                {
                    Name = name;
                }
            }
            """;

        // Act
        var result = await new CSharpNullableContextChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_pass_code_with_nullable_enable_directive()
    {
        // Arrange
        var source = """
            #nullable enable

            public class MyClass
            {
                public string? Name { get; set; }
            }
            """;

        // Act
        var result = await new CSharpNullableContextChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_reject_nullable_disable_directive()
    {
        // Arrange
        var source = """
            #nullable disable

            public class MyClass
            {
                public string Name { get; set; }
            }
            """;

        // Act
        var result = await new CSharpNullableContextChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("#nullable disable", result);
        });
    }

    [Fact]
    public async Task Should_reject_null_forgiving_operator()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                private string? _name;

                public string GetName()
                {
                    return _name!;
                }
            }
            """;

        // Act
        var result = await new CSharpNullableContextChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("null-forgiving operator '!'", result);
        });
    }

    [Fact]
    public async Task Should_reject_null_forgiving_operator_in_expression()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public int GetLength(string? text)
                {
                    return text!.Length;
                }
            }
            """;

        // Act
        var result = await new CSharpNullableContextChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("null-forgiving operator '!'", result);
        });
    }

    [Fact]
    public async Task Should_report_multiple_violations()
    {
        // Arrange
        var source = """
            #nullable disable

            public class MyClass
            {
                private string? _name;

                public string GetName()
                {
                    return _name!;
                }
            }
            """;

        // Act
        var result = await new CSharpNullableContextChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("2 nullable context violation(s)", result);
            Assert.Contains("#nullable disable", result);
            Assert.Contains("null-forgiving operator '!'", result);
        });
    }

    [Fact]
    public async Task Should_report_line_number_for_nullable_disable()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                #nullable disable
                public string Name { get; set; }
                #nullable restore
            }
            """;

        // Act
        var result = await new CSharpNullableContextChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("Line 3", result);
        });
    }

    [Fact]
    public async Task Should_report_line_number_for_null_forgiving_operator()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                private string? _name;

                public string GetName()
                {
                    return _name!;
                }
            }
            """;

        // Act
        var result = await new CSharpNullableContextChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("Line 7", result);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Should_throw_on_empty_or_whitespace_input(string input)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => new CSharpNullableContextChecker().CheckAsync(input));
    }

    [Fact]
    public async Task Should_throw_on_null_input()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => new CSharpNullableContextChecker().CheckAsync(null!));
    }
}
