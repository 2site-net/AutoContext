namespace SharpPilot.Mcp.DotNet.Tests.Tools.DotNet.CSharp;

using SharpPilot.Mcp.DotNet.Tools.Checkers.DotNet.CSharp;

public sealed class CSharpCodingStyleCheckerTests
{
    [Fact]
    public async Task Should_pass_well_formatted_code()
    {
        // Arrange
        var source = """
            /// <summary>
            /// A sample class.
            /// </summary>
            public class MyClass
            {
                private int _value;

                /// <summary>
                /// Does work.
                /// </summary>
                public void DoWork()
                {
                    var x = 1;

                    if (x > 0)
                    {
                        _value = x;
                    }
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_reject_region_directives()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                #region Fields
                private int _value;
                #endregion
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("#region", result);
        });
    }

    [Theory]
    [InlineData("// ── Lifecycle ──────")]
    [InlineData("// ═══════════════════")]
    [InlineData("// -------------------")]
    [InlineData("// ━━━━━━━━━━━━━━━━━━━")]
    [InlineData("// ___________________")]
    public async Task Should_reject_decorative_comments(string comment)
    {
        // Arrange
        var source = $$"""
            public class MyClass
            {
                {{comment}}
                public void DoWork() { }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("Decorative", result);
        });
    }

    [Fact]
    public async Task Should_not_flag_normal_comments()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                // This is a normal comment
                public void DoWork() { }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("Decorative", result);
    }

    [Fact]
    public async Task Should_reject_if_without_curly_braces()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork()
                {
                    int x = 1;

                    if (x > 0)
                        x = 2;
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("curly braces", result);
        });
    }

    [Fact]
    public async Task Should_allow_guard_clause_without_braces()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork(int x)
                {
                    if (x < 0)
                        return;

                    var y = x * 2;
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("curly braces", result);
    }

    [Fact]
    public async Task Should_allow_guard_clause_with_throw()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork(object obj)
                {
                    if (obj is null)
                        throw new ArgumentNullException(nameof(obj));

                    obj.ToString();
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("curly braces", result);
    }

    [Fact]
    public async Task Should_reject_guard_clause_with_else()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork(int x)
                {
                    if (x < 0)
                        return;
                    else
                        x = 2;
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("curly braces", result);
        });
    }

    [Fact]
    public async Task Should_reject_foreach_without_braces()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork()
                {
                    var items = new[] { 1, 2, 3 };

                    foreach (var item in items)
                        Console.WriteLine(item);
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("curly braces", result);
            Assert.Contains("foreach", result);
        });
    }

    [Fact]
    public async Task Should_reject_while_without_braces()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork()
                {
                    int x = 0;

                    while (x < 10)
                        x++;
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("while", result);
        });
    }

    [Fact]
    public async Task Should_reject_missing_blank_line_before_if()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork()
                {
                    var x = 1;
                    if (x > 0)
                    {
                        x = 2;
                    }
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("blank line before", result);
            Assert.Contains("if", result);
        });
    }

    [Fact]
    public async Task Should_pass_if_at_start_of_block()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork()
                {
                    if (true)
                    {
                    }
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("blank line before", result);
    }

    [Fact]
    public async Task Should_reject_expression_body_arrow_on_same_line()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public int GetValue() => 42;
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("Expression-body arrow", result);
            Assert.Contains("next line", result);
        });
    }

    [Fact]
    public async Task Should_pass_expression_body_arrow_on_next_line()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public int GetValue()
                    => 42;
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("Expression-body arrow", result);
    }

    [Fact]
    public async Task Should_reject_property_expression_body_on_same_line()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                private int _value;

                public int Value => _value;
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("Expression-body arrow", result);
        });
    }

    [Fact]
    public async Task Should_pass_property_expression_body_on_next_line()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                private int _value;

                public int Value
                    => _value;
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("Expression-body arrow", result);
    }

    [Fact]
    public async Task Should_not_flag_lambda_expression_body()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork()
                {
                    var fn = (int x) => x * 2;
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("Expression-body arrow", result);
    }

    [Fact]
    public async Task Should_report_multiple_violations()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                #region Fields
                private int _value;
                #endregion

                public int GetValue() => _value;
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("#region", result);
            Assert.Contains("Expression-body arrow", result);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Should_throw_on_empty_or_whitespace_input(string input)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => new CSharpCodingStyleChecker().CheckAsync(input));
    }

    [Fact]
    public async Task Should_throw_on_null_input()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => new CSharpCodingStyleChecker().CheckAsync(null!));
    }

    [Fact]
    public async Task Should_reject_for_without_braces()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork()
                {
                    for (int i = 0; i < 10; i++)
                        Console.WriteLine(i);
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("curly braces", result);
            Assert.Contains("for", result);
        });
    }

    [Fact]
    public async Task Should_reject_missing_blank_line_before_foreach()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork()
                {
                    var items = new[] { 1, 2 };
                    foreach (var item in items)
                    {
                    }
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("blank line before", result);
            Assert.Contains("foreach", result);
        });
    }

    [Fact]
    public async Task Should_reject_using_without_braces()
    {
        // Arrange
        var source = """
            using System;
            using System.IO;

            public class MyClass
            {
                public void DoWork()
                {
                    using (var stream = new MemoryStream())
                        stream.WriteByte(0);
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("curly braces", result);
            Assert.Contains("using", result);
        });
    }

    [Fact]
    public async Task Should_pass_using_with_braces()
    {
        // Arrange
        var source = """
            using System;
            using System.IO;

            public class MyClass
            {
                public void DoWork()
                {
                    using (var stream = new MemoryStream())
                    {
                        stream.WriteByte(0);
                    }
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("curly braces", result);
    }

    [Fact]
    public async Task Should_reject_lock_without_braces()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                private readonly object _sync = new();
                private int _count;

                public void DoWork()
                {
                    lock (_sync)
                        _count++;
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("curly braces", result);
            Assert.Contains("lock", result);
        });
    }

    [Fact]
    public async Task Should_pass_lock_with_braces()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                private readonly object _sync = new();
                private int _count;

                public void DoWork()
                {
                    lock (_sync)
                    {
                        _count++;
                    }
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("curly braces", result);
    }

    [Fact]
    public async Task Should_reject_fixed_without_braces()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public unsafe void DoWork()
                {
                    var arr = new int[] { 1, 2, 3 };

                    fixed (int* p = arr)
                        *p = 42;
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("curly braces", result);
            Assert.Contains("fixed", result);
        });
    }

    [Fact]
    public async Task Should_reject_missing_blank_line_before_using()
    {
        // Arrange
        var source = """
            using System;
            using System.IO;

            public class MyClass
            {
                public void DoWork()
                {
                    var x = 1;
                    using (var stream = new MemoryStream())
                    {
                    }
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("blank line before", result);
            Assert.Contains("using", result);
        });
    }

    [Fact]
    public async Task Should_reject_missing_blank_line_before_lock()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                private readonly object _sync = new();

                public void DoWork()
                {
                    var x = 1;
                    lock (_sync)
                    {
                    }
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("blank line before", result);
            Assert.Contains("lock", result);
        });
    }

    [Fact]
    public async Task Should_reject_missing_blank_line_before_try()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork()
                {
                    var x = 1;
                    try
                    {
                        x = 2;
                    }
                    catch
                    {
                    }
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("blank line before", result);
            Assert.Contains("try", result);
        });
    }

    [Fact]
    public async Task Should_pass_try_with_blank_line_before()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork()
                {
                    var x = 1;

                    try
                    {
                        x = 2;
                    }
                    catch
                    {
                    }
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("blank line before", result);
    }

    [Fact]
    public async Task Should_reject_public_class_without_xml_doc()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                private void DoWork() { }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("XML doc", result);
            Assert.Contains("MyClass", result);
        });
    }

    [Fact]
    public async Task Should_reject_public_method_without_xml_doc()
    {
        // Arrange
        var source = """
            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                public void DoWork() { }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("XML doc", result);
            Assert.Contains("DoWork", result);
        });
    }

    [Fact]
    public async Task Should_reject_protected_method_without_xml_doc()
    {
        // Arrange
        var source = """
            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                protected void Helper() { }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("XML doc", result);
            Assert.Contains("Helper", result);
        });
    }

    [Fact]
    public async Task Should_pass_private_method_without_xml_doc()
    {
        // Arrange
        var source = """
            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                private void Helper() { }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("XML doc", result);
    }

    [Fact]
    public async Task Should_pass_internal_method_without_xml_doc()
    {
        // Arrange
        var source = """
            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                internal void Helper() { }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("XML doc", result);
    }

    [Fact]
    public async Task Should_pass_override_method_without_xml_doc()
    {
        // Arrange
        var source = """
            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                public override string ToString() => "x";
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("XML doc", result);
    }

    [Fact]
    public async Task Should_skip_xml_doc_check_for_test_class()
    {
        // Arrange
        var source = """
            public class MyTests
            {
                public void DoWork() { }

                [Fact]
                public async Task Should_work() { }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("XML doc", result);
    }

    [Fact]
    public async Task Should_pass_public_property_with_xml_doc()
    {
        // Arrange
        var source = """
            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                /// <summary>
                /// Gets or sets the name.
                /// </summary>
                public string Name { get; set; }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("XML doc", result);
    }

    [Fact]
    public async Task Should_reject_public_property_without_xml_doc()
    {
        // Arrange
        var source = """
            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                public string Name { get; set; }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("XML doc", result);
            Assert.Contains("Name", result);
        });
    }

    [Fact]
    public async Task Should_reject_public_constructor_without_xml_doc()
    {
        // Arrange
        var source = """
            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                public MyClass() { }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("XML doc", result);
            Assert.Contains("MyClass", result);
        });
    }

    [Fact]
    public async Task Should_reject_do_without_curly_braces()
    {
        // Arrange
        var source = """
            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                /// <summary>
                /// Does work.
                /// </summary>
                public void DoWork()
                {
                    var x = 0;

                    do
                        x++;
                    while (x < 10);
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("curly braces", result);
            Assert.Contains("'do'", result);
        });
    }

    [Fact]
    public async Task Should_reject_missing_blank_line_before_switch()
    {
        // Arrange
        var source = """
            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                /// <summary>
                /// Does work.
                /// </summary>
                public void DoWork(int x)
                {
                    var y = 0;
                    switch (x)
                    {
                        case 1:
                            y = 1;
                            break;
                    }
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("blank line", result);
            Assert.Contains("'switch'", result);
        });
    }

    [Fact]
    public async Task Should_not_flag_else_if_for_blank_line()
    {
        // Arrange
        var source = """
            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                /// <summary>
                /// Does work.
                /// </summary>
                public void DoWork(int x)
                {
                    var y = 0;

                    if (x == 1)
                    {
                        y = 1;
                    }
                    else if (x == 2)
                    {
                        y = 2;
                    }
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("blank line", result);
    }

    [Fact]
    public async Task Should_reject_public_enum_without_xml_doc()
    {
        // Arrange
        var source = """
            public enum Status
            {
                Active,
                Inactive,
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("XML doc", result);
            Assert.Contains("Status", result);
        });
    }

    [Fact]
    public async Task Should_reject_public_delegate_without_xml_doc()
    {
        // Arrange
        var source = """
            public delegate void MyHandler(object sender);
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("XML doc", result);
            Assert.Contains("MyHandler", result);
        });
    }

    [Fact]
    public async Task Should_flag_unnecessary_braces_when_editorconfig_false()
    {
        // Arrange — single-line body with braces
        var source = """
            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                /// <summary>
                /// Does work.
                /// </summary>
                public void DoWork()
                {
                    var items = new[] { 1, 2, 3 };

                    foreach (var item in items)
                    {
                        Console.WriteLine(item);
                    }
                }
            }
            """;

        var data = new Dictionary<string, string> { ["csharp_prefer_braces"] = "false" };

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source, data);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("unnecessary curly braces", result, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Should_pass_braceless_body_when_editorconfig_false()
    {
        // Arrange — no braces on single-line body
        var source = """
            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                /// <summary>
                /// Does work.
                /// </summary>
                public void DoWork()
                {
                    var items = new[] { 1, 2, 3 };

                    foreach (var item in items)
                        Console.WriteLine(item);
                }
            }
            """;

        var data = new Dictionary<string, string> { ["csharp_prefer_braces"] = "false" };

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source, data);

        // Assert
        Assert.DoesNotContain("curly braces", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_flag_unnecessary_braces_on_single_line_when_editorconfig_when_multiline()
    {
        // Arrange — single-line body with braces
        var source = """
            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                /// <summary>
                /// Does work.
                /// </summary>
                public void DoWork()
                {
                    var items = new[] { 1, 2, 3 };

                    foreach (var item in items)
                    {
                        Console.WriteLine(item);
                    }
                }
            }
            """;

        var data = new Dictionary<string, string> { ["csharp_prefer_braces"] = "when_multiline" };

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source, data);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("unnecessary curly braces", result, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Should_require_braces_on_multiline_when_editorconfig_when_multiline()
    {
        // Arrange — multi-line body without braces
        var source = """
            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                /// <summary>
                /// Does work.
                /// </summary>
                public void DoWork()
                {
                    var items = new[] { 1, 2, 3 };

                    foreach (var item in items)
                        Console.WriteLine(
                            item);
                }
            }
            """;

        var data = new Dictionary<string, string> { ["csharp_prefer_braces"] = "when_multiline" };

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source, data);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("requires curly braces", result, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("multi-line", result, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Should_enforce_curly_braces_when_editorconfig_says_true()
    {
        // Arrange — missing braces on if
        var source = """
            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                /// <summary>
                /// Does work.
                /// </summary>
                public void DoWork()
                {
                    var items = new[] { 1, 2, 3 };

                    foreach (var item in items)
                        Console.WriteLine(item);
                }
            }
            """;

        var data = new Dictionary<string, string> { ["csharp_prefer_braces"] = "true" };

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source, data);

        // Assert — should still enforce braces
        Assert.Contains("curly braces", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_enforce_curly_braces_when_no_editorconfig()
    {
        // Arrange — missing braces on foreach, no data
        var source = """
            /// <summary>
            /// A class.
            /// </summary>
            public class MyClass
            {
                /// <summary>
                /// Does work.
                /// </summary>
                public void DoWork()
                {
                    var items = new[] { 1, 2, 3 };

                    foreach (var item in items)
                        Console.WriteLine(item);
                }
            }
            """;

        // Act
        var result = await new CSharpCodingStyleChecker().CheckAsync(source);

        // Assert — default behavior: enforce braces
        Assert.Contains("curly braces", result, StringComparison.OrdinalIgnoreCase);
    }
}
