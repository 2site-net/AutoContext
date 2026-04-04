namespace SharpPilot.Mcp.DotNet.Tests.Tools.Checkers.CSharp;

using SharpPilot.Mcp.DotNet.Tools.Checkers.CSharp;

public sealed class CSharpMemberOrderingCheckerTests
{
    [Fact]
    public async Task Should_pass_correctly_ordered_members()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public const int MaxSize = 100;
                private const int MinSize = 1;

                public static readonly string Default = "x";
                private readonly int _value;

                public MyClass(int value) => _value = value;

                public string Name { get; set; }
                private int Id { get; set; }

                public static void Reset() { }
                public void DoWork() { }
                private void Helper() { }

                private class Inner { }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_reject_field_before_constant()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                private int _value;
                public const int MaxSize = 100;
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("constant", result);
            Assert.Contains("field", result);
        });
    }

    [Fact]
    public async Task Should_reject_method_before_property()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork() { }
                public string Name { get; set; }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("property", result);
            Assert.Contains("method", result);
        });
    }

    [Fact]
    public async Task Should_reject_constructor_before_field()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public MyClass() { }
                private int _value;
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("field", result);
            Assert.Contains("constructor", result);
        });
    }

    [Fact]
    public async Task Should_reject_nested_type_before_method()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                private class Inner { }
                public void DoWork() { }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("method", result);
            Assert.Contains("nested type", result);
        });
    }

    [Fact]
    public async Task Should_reject_private_before_public_in_same_kind()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                private void Helper() { }
                public void DoWork() { }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("public", result);
            Assert.Contains("private", result);
        });
    }

    [Fact]
    public async Task Should_reject_instance_before_static_in_same_kind_and_access()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork() { }
                public static void Reset() { }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("static", result);
            Assert.Contains("instance", result);
        });
    }

    [Fact]
    public async Task Should_report_multiple_violations()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork() { }
                private int _value;
                public const int MaxSize = 100;
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("2 ordering violation", result);
        });
    }

    [Fact]
    public async Task Should_check_each_type_independently()
    {
        // Arrange
        var source = """
            public class First
            {
                public const int X = 1;
                private int _y;
            }

            public class Second
            {
                private int _z;
                public const int W = 2;
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("Second", result);
            Assert.DoesNotContain("First", result);
        });
    }

    [Fact]
    public async Task Should_pass_struct_with_correct_ordering()
    {
        // Arrange
        var source = """
            public struct Point
            {
                public int X { get; set; }
                public int Y { get; set; }

                public double Distance() => 0;
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_pass_record_with_correct_ordering()
    {
        // Arrange
        var source = """
            public record Person
            {
                public int Age { get; init; }
                public string Name { get; init; }

                public string Greet() => $"Hi, {Name}";
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_treat_internal_between_public_and_protected()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                protected void Helper() { }
                internal void Other() { }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("internal", result);
            Assert.Contains("protected", result);
        });
    }

    [Fact]
    public async Task Should_pass_event_before_method()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public event EventHandler? Changed;
                public void DoWork() { }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Should_throw_on_empty_or_whitespace_input(string input)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => new CSharpMemberOrderingChecker().CheckAsync(input));
    }

    [Fact]
    public async Task Should_throw_on_null_input()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => new CSharpMemberOrderingChecker().CheckAsync(null!));
    }

    [Fact]
    public async Task Should_pass_empty_class()
    {
        // Arrange
        var source = """
            public class Empty { }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_pass_property_before_indexer_before_method()
    {
        // Arrange
        var source = """
            public class MyList
            {
                public string Name { get; set; }

                public int this[int index] => index;

                public void Clear() { }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_reject_indexer_before_property()
    {
        // Arrange
        var source = """
            public class MyList
            {
                public int this[int index] => index;
                public string Name { get; set; }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("property", result);
            Assert.Contains("indexer", result);
        });
    }

    [Fact]
    public async Task Should_reject_instance_field_before_static_field()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                private int _value;
                private static int _counter;
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("static field", result);
            Assert.Contains("field", result);
        });
    }

    [Fact]
    public async Task Should_pass_delegate_before_event()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public delegate void MyHandler();

                public event EventHandler? Changed;
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_reject_event_before_delegate()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public event EventHandler? Changed;
                public delegate void MyHandler();
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("delegate", result);
            Assert.Contains("event", result);
        });
    }

    [Fact]
    public async Task Should_pass_enum_before_property()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public enum Status { Active, Inactive }

                public int Value { get; set; }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_pass_operator_after_method()
    {
        // Arrange
        var source = """
            public class Money
            {
                public int Amount { get; set; }

                public override string ToString() => Amount.ToString();

                public static Money operator +(Money a, Money b) => new();
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_reject_operator_before_method()
    {
        // Arrange
        var source = """
            public class Money
            {
                public static Money operator +(Money a, Money b) => new();
                public override string ToString() => Amount.ToString();

                public int Amount { get; set; }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("❌", result);
    }

    [Fact]
    public async Task Should_skip_test_class_with_fact_attribute()
    {
        // Arrange
        var source = """
            using Xunit;

            public class MyTests
            {
                public void DoWork() { }
                private int _value;

                [Fact]
                public async Task Should_work() { }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_skip_test_class_with_theory_attribute()
    {
        // Arrange
        var source = """
            using Xunit;

            public class MyTests
            {
                public void DoWork() { }
                private int _value;

                [Theory]
                public async Task Should_work(int x) { }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_skip_test_class_with_test_attribute()
    {
        // Arrange
        var source = """
            using NUnit.Framework;

            public class MyTests
            {
                public void DoWork() { }
                private int _value;

                [Test]
                public async Task Should_work() { }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_skip_test_class_with_testcase_attribute()
    {
        // Arrange
        var source = """
            using NUnit.Framework;

            public class MyTests
            {
                public void DoWork() { }
                private int _value;

                [TestCase(1)]
                public async Task Should_work(int x) { }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_pass_full_resharper_ordering()
    {
        // Arrange
        var source = """
            public class FullOrder
            {
                public const int MaxSize = 100;

                private static readonly string _default = "x";

                private readonly int _value;

                public FullOrder(int value) => _value = value;

                public delegate void MyHandler();

                public event EventHandler? Changed;

                public enum Status { Active }

                public string Name { get; set; }

                public int this[int index] => index;

                public void DoWork() { }

                public static FullOrder operator +(FullOrder a, FullOrder b) => new();

                private class Inner { }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_reject_non_alphabetical_methods_in_same_group()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void Zebra() { }
                public void Apple() { }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("alphabetical", result);
            Assert.Contains("Apple", result);
            Assert.Contains("Zebra", result);
        });
    }

    [Fact]
    public async Task Should_pass_alphabetical_methods_in_same_group()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void Alpha() { }
                public void Beta() { }
                public void Gamma() { }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_reject_non_alphabetical_properties_in_same_group()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public string Name { get; set; }
                public int Age { get; set; }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("alphabetical", result);
            Assert.Contains("Age", result);
        });
    }

    [Fact]
    public async Task Should_not_compare_alphabetically_across_access_levels()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void Zebra() { }
                private void Apple() { }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("alphabetical", result);
    }

    [Fact]
    public async Task Should_pass_event_declaration_before_method()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public event EventHandler Changed
                {
                    add { }
                    remove { }
                }

                public void DoWork() { }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("should appear before", result);
    }

    [Fact]
    public async Task Should_reject_method_before_event_declaration()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork() { }

                public event EventHandler Changed
                {
                    add { }
                    remove { }
                }
            }
            """;

        // Act
        var result = await new CSharpMemberOrderingChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("event", result);
            Assert.Contains("should appear before", result);
        });
    }
}
