namespace AutoContext.Worker.DotNet.Tests.Tasks.CSharp;

using AutoContext.Worker.DotNet.Tasks.CSharp;
using AutoContext.Worker.Testing;

public sealed class AnalyzeCSharpNamingConventionsTaskTests
{
    [Fact]
    public async Task Should_pass_well_named_code()
    {
        // Arrange
        var source = """
            public interface IRepository { }

            public static class StringExtensions
            {
                public static string Truncate(this string source, int maxLength)
                    => source;
            }

            public sealed class UserService
            {
                private int _retryCount;

                public string Name { get; set; } = string.Empty;

                public void ProcessUser(string userName) { }

                public async Task LoadUsersAsync() { }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.StartsWith("✅", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_reject_interface_without_I_prefix()
    {
        // Arrange
        var source = """
            public interface DataService { }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("'I'", result, StringComparison.Ordinal);
            Assert.Contains("DataService", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_pass_interface_with_I_prefix()
    {
        // Arrange
        var source = """
            public interface IDataService { }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("Interface", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_reject_interface_with_lowercase_after_I()
    {
        // Arrange
        var source = """
            public interface Iservice { }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("'I' followed by an uppercase letter", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_reject_extension_class_without_Extensions_suffix()
    {
        // Arrange
        var source = """
            public static class StringHelper
            {
                public static string Trim(this string source)
                    => source;
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("Extensions", result, StringComparison.Ordinal);
            Assert.Contains("StringHelper", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_pass_extension_class_with_Extensions_suffix()
    {
        // Arrange
        var source = """
            public static class StringExtensions
            {
                public static string Trim(this string source)
                    => source;
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("StringExtensions", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_pass_static_class_without_extension_methods()
    {
        // Arrange
        var source = """
            public static class MathHelper
            {
                public static int Add(int a, int b)
                    => a + b;
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("Extension", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_reject_async_method_without_Async_suffix()
    {
        // Arrange
        var source = """
            public class MyService
            {
                public async Task LoadData() { }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("'Async'", result, StringComparison.Ordinal);
            Assert.Contains("LoadData", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_pass_async_method_with_Async_suffix()
    {
        // Arrange
        var source = """
            public class MyService
            {
                public async Task LoadDataAsync() { }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("suffixed with 'Async'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_skip_override_async_method_for_Async_suffix()
    {
        // Arrange
        var source = """
            public abstract class Base
            {
                public abstract Task DoWork();
            }

            public class Derived : Base
            {
                public override async Task DoWork() { }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("suffixed with 'Async'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_skip_event_handler_for_Async_suffix()
    {
        // Arrange
        var source = """
            public class MyForm
            {
                public async void OnButtonClicked(object sender, EventArgs e) { }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("suffixed with 'Async'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_skip_test_methods_for_Async_suffix()
    {
        // Arrange
        var source = """
            public class MyTests
            {
                [Fact]
                public async Task Should_do_something() { }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("suffixed with 'Async'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_reject_private_field_without_underscore()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                private int retryCount;
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("_camelCase", result, StringComparison.Ordinal);
            Assert.Contains("retryCount", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_reject_private_field_with_uppercase_after_underscore()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                private int _RetryCount;
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("_camelCase", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_pass_private_field_with_underscore_camelCase()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                private int _retryCount;
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("_camelCase", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_skip_static_private_field_naming()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                private static int Count;
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("_camelCase", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_skip_const_field_naming()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                private const int MaxRetries = 3;
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("_camelCase", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_reject_type_starting_with_lowercase()
    {
        // Arrange
        var source = """
            public class myService { }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("PascalCase", result, StringComparison.Ordinal);
            Assert.Contains("myService", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_reject_method_starting_with_lowercase()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void doWork() { }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("PascalCase", result, StringComparison.Ordinal);
            Assert.Contains("doWork", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_reject_property_starting_with_lowercase()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public int value { get; set; }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("PascalCase", result, StringComparison.Ordinal);
            Assert.Contains("value", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_reject_parameter_starting_with_uppercase()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork(int Value) { }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("camelCase", result, StringComparison.Ordinal);
            Assert.Contains("Value", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_reject_parameter_with_leading_underscore()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork(int _value) { }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("camelCase", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_pass_camelCase_parameter()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void DoWork(int value, string inputText) { }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("camelCase", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_skip_this_parameter_in_extension_method()
    {
        // Arrange
        var source = """
            public static class StringExtensions
            {
                public static int GetLength(this string Source)
                    => Source.Length;
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("camelCase", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_skip_discard_parameter()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public void Process(string _) { }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("camelCase", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_reject_delegate_with_lowercase_name()
    {
        // Arrange
        var source = """
            public delegate void myCallback(int result);
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("PascalCase", result, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Should_throw_on_empty_or_whitespace_input(string input)
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AnalyzeCSharpNamingConventionsTask().ExecuteAsync(new { content = input }));
    }

    [Fact]
    public async Task Should_throw_on_null_input()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AnalyzeCSharpNamingConventionsTask().ExecuteAsync(new { content = (string?)null }));
    }

    [Fact]
    public async Task Should_reject_event_field_with_lowercase_name()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public event EventHandler? changed;
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("PascalCase", result, StringComparison.Ordinal);
            Assert.Contains("changed", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_reject_event_declaration_with_lowercase_name()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public event EventHandler changed
                {
                    add { }
                    remove { }
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("PascalCase", result, StringComparison.Ordinal);
            Assert.Contains("changed", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_pass_event_field_with_PascalCase_name()
    {
        // Arrange
        var source = """
            public class MyClass
            {
                public event EventHandler? Changed;
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpNamingConventionsTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("PascalCase", result, StringComparison.Ordinal);
    }
}
