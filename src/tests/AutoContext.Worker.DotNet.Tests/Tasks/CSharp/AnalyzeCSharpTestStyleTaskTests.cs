namespace AutoContext.Worker.DotNet.Tests.Tasks.CSharp;

using AutoContext.Worker.DotNet.Tasks.CSharp;
using AutoContext.Worker.Testing;

public sealed class AnalyzeCSharpTestStyleTaskTests
{
    [Fact]
    public async Task Should_pass_well_styled_test_class()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public async Task Should_return_user_by_id()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.StartsWith("✅", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_reject_test_class_without_tests_suffix()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTest
            {
                [Fact]
                public async Task Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("must be suffixed with 'Tests'", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_pass_test_class_with_tests_suffix()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public async Task Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("suffixed", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_reject_test_method_without_should_prefix()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public void TestGetUser()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("must start with 'Should_'", result, StringComparison.Ordinal);
            Assert.Contains("'TestGetUser'", result, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("Should_return_data")]
    [InlineData("Should_not_throw")]
    public async Task Should_pass_test_method_with_valid_prefix(string methodName)
    {
        // Arrange
        var source = $$"""
            public sealed class SomeTests
            {
                [Fact]
                public void {{methodName}}()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("must start with", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_reject_xml_doc_on_test_class()
    {
        // Arrange
        var source = """
            /// <summary>Tests for UserService.</summary>
            public sealed class UserServiceTests
            {
                [Fact]
                public async Task Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("should not have XML doc comments", result, StringComparison.Ordinal);
            Assert.Contains("'UserServiceTests'", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_reject_xml_doc_on_test_method()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                /// <summary>Checks user retrieval.</summary>
                [Fact]
                public async Task Should_get_user()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("should not have XML doc comments", result, StringComparison.Ordinal);
            Assert.Contains("'Should_get_user'", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_pass_test_without_xml_doc()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public async Task Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("XML doc", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_reject_multiple_asserts_without_assert_multiple()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public async Task Should_check_user()
                {
                    Assert.NotNull("x");
                    Assert.Equal(1, 1);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("Assert.Multiple()", result, StringComparison.Ordinal);
            Assert.Contains("2 Assert calls", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_pass_multiple_asserts_inside_assert_multiple()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public async Task Should_check_user()
                {
                    Assert.Multiple(() =>
                    {
                        Assert.NotNull("x");
                        Assert.Equal(1, 1);
                    });
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("Assert.Multiple", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_pass_single_assert_without_assert_multiple()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public async Task Should_check_user()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("Assert.Multiple", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_reject_configure_await_in_test()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public async Task Should_get_data()
                {
                    await Task.Delay(1).ConfigureAwait(false);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("ConfigureAwait()", result, StringComparison.Ordinal);
            Assert.Contains("xUnit1030", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_pass_async_test_without_configure_await()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public async Task Should_get_data()
                {
                    await Task.Delay(1);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("ConfigureAwait", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_report_multiple_violations()
    {
        // Arrange
        var source = """
            /// <summary>Bad test class.</summary>
            public sealed class UserServiceTest
            {
                /// <summary>Bad method.</summary>
                [Fact]
                public void GetUser()
                {
                    Assert.NotNull("x");
                    Assert.Equal(1, 1);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("suffixed with 'Tests'", result, StringComparison.Ordinal);
            Assert.Contains("must start with 'Should_'", result, StringComparison.Ordinal);
            Assert.Contains("Assert.Multiple()", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_skip_non_test_classes()
    {
        // Arrange
        var source = """
            public class Helper
            {
                public void DoWork()
                {
                    var x = 1;
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.StartsWith("✅", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_detect_theory_test_methods()
    {
        // Arrange
        var source = """
            public sealed class CalcTests
            {
                [Theory]
                [InlineData(1)]
                public void InvalidName(int x)
                {
                    Assert.Equal(1, x);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.Contains("must start with 'Should_'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_not_flag_non_test_methods_in_test_class()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public async Task Should_work()
                {
                    Assert.True(true);
                }

                private static int Helper() => 42;
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.StartsWith("✅", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_reject_file_name_not_ending_with_tests()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public async Task Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new Dictionary<string, object> { ["content"] = source, ["comparedPath"] = "UserService.cs" });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("must end with 'Tests' before the extension", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_pass_file_name_ending_with_tests()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public async Task Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new Dictionary<string, object> { ["content"] = source, ["comparedPath"] = "UserServiceTests.cs" });

        // Assert
        Assert.StartsWith("✅", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("UserServiceTests.razor.cs")]
    [InlineData("UserServiceTests.razor")]
    public async Task Should_pass_razor_file_name_ending_with_tests(string fileName)
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public async Task Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new Dictionary<string, object> { ["content"] = source, ["comparedPath"] = fileName });

        // Assert
        Assert.StartsWith("✅", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("UserService.razor.cs")]
    [InlineData("UserService.razor")]
    public async Task Should_reject_razor_file_name_not_ending_with_tests(string fileName)
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public async Task Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new Dictionary<string, object> { ["content"] = source, ["comparedPath"] = fileName });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("must end with 'Tests' before the extension", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_reject_namespace_not_mirroring_production()
    {
        // Arrange
        var source = """
            namespace Wrong.Namespace;

            public sealed class UserServiceTests
            {
                [Fact]
                public async Task Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new Dictionary<string, object> { ["content"] = source, ["originalNamespace"] = "MyApp.Services" });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("does not mirror the production namespace", result, StringComparison.Ordinal);
            Assert.Contains("Expected 'MyApp.Tests.Services'", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_pass_namespace_mirroring_production()
    {
        // Arrange
        var source = """
            namespace MyApp.Tests.Services;

            public sealed class UserServiceTests
            {
                [Fact]
                public async Task Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new Dictionary<string, object> { ["content"] = source, ["originalNamespace"] = "MyApp.Services" });

        // Assert
        Assert.StartsWith("✅", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_handle_single_segment_production_namespace()
    {
        // Arrange
        var source = """
            namespace MyApp.Tests;

            public sealed class HelperTests
            {
                [Fact]
                public async Task Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new Dictionary<string, object> { ["content"] = source, ["originalNamespace"] = "MyApp" });

        // Assert
        Assert.StartsWith("✅", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_reject_deep_namespace_mismatch()
    {
        // Arrange
        var source = """
            namespace MyApp.Tests.Wrong;

            public sealed class RepoTests
            {
                [Fact]
                public async Task Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new Dictionary<string, object> { ["content"] = source, ["originalNamespace"] = "MyApp.Data.Repositories" });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result, StringComparison.Ordinal);
            Assert.Contains("Expected 'MyApp.Tests.Data.Repositories'", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Should_pass_deep_namespace_mirroring()
    {
        // Arrange
        var source = """
            namespace MyApp.Tests.Data.Repositories;

            public sealed class UserRepoTests
            {
                [Fact]
                public async Task Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new Dictionary<string, object> { ["content"] = source, ["originalNamespace"] = "MyApp.Data.Repositories" });

        // Assert
        Assert.StartsWith("✅", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_skip_namespace_check_when_not_provided()
    {
        // Arrange
        var source = """
            namespace Anything.Here;

            public sealed class SomeTests
            {
                [Fact]
                public async Task Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new { content = source });

        // Assert
        Assert.DoesNotContain("mirror", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_check_namespace_with_block_scoped_namespace()
    {
        // Arrange
        var source = """
            namespace Wrong.Namespace
            {
                public sealed class SomeTests
                {
                    [Fact]
                    public async Task Should_work()
                    {
                        Assert.True(true);
                    }
                }
            }
            """;

        // Act
        var (_, result) = await new AnalyzeCSharpTestStyleTask().GetReportAsync(new Dictionary<string, object> { ["content"] = source, ["originalNamespace"] = "MyApp.Services" });

        // Assert
        Assert.Contains("does not mirror", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Should_throw_on_empty_or_whitespace_input(string input)
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new AnalyzeCSharpTestStyleTask().ExecuteAsync(new { content = input }));
    }

    [Fact]
    public async Task Should_throw_on_null_input()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new AnalyzeCSharpTestStyleTask().ExecuteAsync(new { content = (string?)null }));
    }
}
