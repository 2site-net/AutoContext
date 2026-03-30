namespace SharpPilot.Mcp.DotNet.Tests.Tools.DotNet.CSharp;

using System.Text.Json.Nodes;

using SharpPilot.Mcp.DotNet.Tools.Checkers.DotNet.CSharp;

public sealed class CSharpTestStyleCheckerTests
{
    [Fact]
    public void Should_pass_well_styled_test_class()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public void Should_return_user_by_id()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_reject_test_class_without_tests_suffix()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTest
            {
                [Fact]
                public void Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("must be suffixed with 'Tests'", result);
        });
    }

    [Fact]
    public void Should_pass_test_class_with_tests_suffix()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public void Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.DoesNotContain("suffixed", result);
    }

    [Fact]
    public void Should_reject_test_method_without_should_prefix()
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
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("must start with 'Should_'", result);
            Assert.Contains("'TestGetUser'", result);
        });
    }

    [Theory]
    [InlineData("Should_return_data")]
    [InlineData("Should_not_throw")]
    public void Should_pass_test_method_with_valid_prefix(string methodName)
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
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.DoesNotContain("must start with", result);
    }

    [Fact]
    public void Should_reject_xml_doc_on_test_class()
    {
        // Arrange
        var source = """
            /// <summary>Tests for UserService.</summary>
            public sealed class UserServiceTests
            {
                [Fact]
                public void Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("should not have XML doc comments", result);
            Assert.Contains("'UserServiceTests'", result);
        });
    }

    [Fact]
    public void Should_reject_xml_doc_on_test_method()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                /// <summary>Checks user retrieval.</summary>
                [Fact]
                public void Should_get_user()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("should not have XML doc comments", result);
            Assert.Contains("'Should_get_user'", result);
        });
    }

    [Fact]
    public void Should_pass_test_without_xml_doc()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public void Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.DoesNotContain("XML doc", result);
    }

    [Fact]
    public void Should_reject_multiple_asserts_without_assert_multiple()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public void Should_check_user()
                {
                    Assert.NotNull("x");
                    Assert.Equal(1, 1);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("Assert.Multiple()", result);
            Assert.Contains("2 Assert calls", result);
        });
    }

    [Fact]
    public void Should_pass_multiple_asserts_inside_assert_multiple()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public void Should_check_user()
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
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.DoesNotContain("Assert.Multiple", result);
    }

    [Fact]
    public void Should_pass_single_assert_without_assert_multiple()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public void Should_check_user()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.DoesNotContain("Assert.Multiple", result);
    }

    [Fact]
    public void Should_reject_configure_await_in_test()
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
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("ConfigureAwait()", result);
            Assert.Contains("xUnit1030", result);
        });
    }

    [Fact]
    public void Should_pass_async_test_without_configure_await()
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
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.DoesNotContain("ConfigureAwait", result);
    }

    [Fact]
    public void Should_report_multiple_violations()
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
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("suffixed with 'Tests'", result);
            Assert.Contains("must start with 'Should_'", result);
            Assert.Contains("Assert.Multiple()", result);
        });
    }

    [Fact]
    public void Should_skip_non_test_classes()
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
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_detect_theory_test_methods()
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
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.Contains("must start with 'Should_'", result);
    }

    [Fact]
    public void Should_not_flag_non_test_methods_in_test_class()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public void Should_work()
                {
                    Assert.True(true);
                }

                private static int Helper() => 42;
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_reject_file_name_not_ending_with_tests()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public void Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source, new JsonObject { ["testFileName"] = "UserService.cs" });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("must end with 'Tests' before the extension", result);
        });
    }

    [Fact]
    public void Should_pass_file_name_ending_with_tests()
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public void Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source, new JsonObject { ["testFileName"] = "UserServiceTests.cs" });

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Theory]
    [InlineData("UserServiceTests.razor.cs")]
    [InlineData("UserServiceTests.razor")]
    public void Should_pass_razor_file_name_ending_with_tests(string fileName)
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public void Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source, new JsonObject { ["testFileName"] = fileName });

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Theory]
    [InlineData("UserService.razor.cs")]
    [InlineData("UserService.razor")]
    public void Should_reject_razor_file_name_not_ending_with_tests(string fileName)
    {
        // Arrange
        var source = """
            public sealed class UserServiceTests
            {
                [Fact]
                public void Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source, new JsonObject { ["testFileName"] = fileName });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("must end with 'Tests' before the extension", result);
        });
    }

    [Fact]
    public void Should_reject_namespace_not_mirroring_production()
    {
        // Arrange
        var source = """
            namespace Wrong.Namespace;

            public sealed class UserServiceTests
            {
                [Fact]
                public void Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source, new JsonObject { ["productionNamespace"] = "MyApp.Services" });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("does not mirror the production namespace", result);
            Assert.Contains("Expected 'MyApp.Tests.Services'", result);
        });
    }

    [Fact]
    public void Should_pass_namespace_mirroring_production()
    {
        // Arrange
        var source = """
            namespace MyApp.Tests.Services;

            public sealed class UserServiceTests
            {
                [Fact]
                public void Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source, new JsonObject { ["productionNamespace"] = "MyApp.Services" });

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_handle_single_segment_production_namespace()
    {
        // Arrange
        var source = """
            namespace MyApp.Tests;

            public sealed class HelperTests
            {
                [Fact]
                public void Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source, new JsonObject { ["productionNamespace"] = "MyApp" });

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_reject_deep_namespace_mismatch()
    {
        // Arrange
        var source = """
            namespace MyApp.Tests.Wrong;

            public sealed class RepoTests
            {
                [Fact]
                public void Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source, new JsonObject { ["productionNamespace"] = "MyApp.Data.Repositories" });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("Expected 'MyApp.Tests.Data.Repositories'", result);
        });
    }

    [Fact]
    public void Should_pass_deep_namespace_mirroring()
    {
        // Arrange
        var source = """
            namespace MyApp.Tests.Data.Repositories;

            public sealed class UserRepoTests
            {
                [Fact]
                public void Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source, new JsonObject { ["productionNamespace"] = "MyApp.Data.Repositories" });

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_skip_namespace_check_when_not_provided()
    {
        // Arrange
        var source = """
            namespace Anything.Here;

            public sealed class SomeTests
            {
                [Fact]
                public void Should_work()
                {
                    Assert.True(true);
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source);

        // Assert
        Assert.DoesNotContain("mirror", result);
    }

    [Fact]
    public void Should_check_namespace_with_block_scoped_namespace()
    {
        // Arrange
        var source = """
            namespace Wrong.Namespace
            {
                public sealed class SomeTests
                {
                    [Fact]
                    public void Should_work()
                    {
                        Assert.True(true);
                    }
                }
            }
            """;

        // Act
        var result = new CSharpTestStyleChecker().Check(source, new JsonObject { ["productionNamespace"] = "MyApp.Services" });

        // Assert
        Assert.Contains("does not mirror", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Should_throw_on_empty_or_whitespace_input(string input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new CSharpTestStyleChecker().Check(input));
    }

    [Fact]
    public void Should_throw_on_null_input()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CSharpTestStyleChecker().Check(null!));
    }
}
