namespace AutoContext.Mcp.DotNet.Tests.Tools.CSharp;

using AutoContext.Mcp.DotNet.Tools.CSharp;

public sealed class CSharpProjectStructureCheckerTests
{
    [Fact]
    public async Task Should_pass_well_structured_file()
    {
        // Arrange
        var source = """
            namespace MyApp.Services;

            public sealed class UserService
            {
                public void DoWork() { }
            }
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source, new Dictionary<string, string> { ["productionFileName"] = "UserService.cs" });

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_pass_without_file_name()
    {
        // Arrange
        var source = """
            namespace MyApp.Services;

            public sealed class UserService
            {
                public void DoWork() { }
            }
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_reject_block_scoped_namespace()
    {
        // Arrange
        var source = """
            namespace MyApp.Services
            {
                public class UserService { }
            }
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("Block-scoped namespace", result);
            Assert.Contains("file-scoped namespace", result);
        });
    }

    [Fact]
    public async Task Should_pass_file_scoped_namespace()
    {
        // Arrange
        var source = """
            namespace MyApp.Services;

            public class UserService { }
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("namespace", result);
    }

    [Fact]
    public async Task Should_reject_multiple_types_in_one_file()
    {
        // Arrange
        var source = """
            namespace MyApp.Models;

            public class User { }

            public class UserDto { }
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("2 top-level type declarations", result);
            Assert.Contains("'User'", result);
            Assert.Contains("'UserDto'", result);
        });
    }

    [Fact]
    public async Task Should_reject_type_and_delegate_in_same_file()
    {
        // Arrange
        var source = """
            namespace MyApp.Models;

            public delegate void MyCallback(int result);

            public class EventProcessor { }
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("2 top-level type declarations", result);
        });
    }

    [Fact]
    public async Task Should_pass_single_type_with_nested_type()
    {
        // Arrange
        var source = """
            namespace MyApp.Models;

            public class Outer
            {
                private class Inner { }
            }
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("top-level type", result);
    }

    [Fact]
    public async Task Should_reject_file_name_mismatch()
    {
        // Arrange
        var source = """
            namespace MyApp.Services;

            public class UserService { }
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source, new Dictionary<string, string> { ["productionFileName"] = "WrongName.cs" });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("'WrongName.cs'", result);
            Assert.Contains("'UserService'", result);
            Assert.Contains("Rename the file to 'UserService.cs'", result);
        });
    }

    [Fact]
    public async Task Should_pass_file_name_matching_type()
    {
        // Arrange
        var source = """
            namespace MyApp.Services;

            public class UserService { }
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source, new Dictionary<string, string> { ["productionFileName"] = "UserService.cs" });

        // Assert
        Assert.DoesNotContain("file name", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_reject_pragma_warning_disable()
    {
        // Arrange
        var source = """
            namespace MyApp.Services;

            #pragma warning disable CA1822
            public class UserService
            {
                public void DoWork() { }
            }
            #pragma warning restore CA1822
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("#pragma warning disable", result);
            Assert.Contains("[SuppressMessage]", result);
        });
    }

    [Fact]
    public async Task Should_pass_pragma_warning_restore()
    {
        // Arrange
        var source = """
            namespace MyApp.Services;

            public class UserService
            {
                #pragma warning restore CA1822
                public void DoWork() { }
            }
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source);

        // Assert
        Assert.DoesNotContain("#pragma", result);
    }

    [Fact]
    public async Task Should_report_multiple_violations()
    {
        // Arrange
        var source = """
            namespace MyApp.Services
            {
                #pragma warning disable CA1822
                public class UserService { }
                public class AnotherService { }
            }
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("Block-scoped namespace", result);
            Assert.Contains("top-level type declarations", result);
            Assert.Contains("#pragma warning disable", result);
        });
    }

    [Fact]
    public async Task Should_not_check_file_name_when_multiple_types()
    {
        // Arrange
        var source = """
            namespace MyApp.Models;

            public class User { }

            public class UserDto { }
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source, new Dictionary<string, string> { ["productionFileName"] = "User.cs" });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.Contains("top-level type declarations", result);
            Assert.DoesNotContain("file name", result, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Should_pass_single_delegate_file()
    {
        // Arrange
        var source = """
            namespace MyApp.Models;

            public delegate void DataCallback(int result);
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source, new Dictionary<string, string> { ["productionFileName"] = "DataCallback.cs" });

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_reject_delegate_file_name_mismatch()
    {
        // Arrange
        var source = """
            namespace MyApp.Models;

            public delegate void DataCallback(int result);
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source, new Dictionary<string, string> { ["productionFileName"] = "Wrong.cs" });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("'DataCallback'", result);
        });
    }

    [Fact]
    public async Task Should_pass_interface_file()
    {
        // Arrange
        var source = """
            namespace MyApp.Abstractions;

            public interface IUserRepository
            {
                void Save();
            }
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source, new Dictionary<string, string> { ["productionFileName"] = "IUserRepository.cs" });

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_pass_enum_file()
    {
        // Arrange
        var source = """
            namespace MyApp.Models;

            public enum Status
            {
                Active,
                Inactive,
            }
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source, new Dictionary<string, string> { ["productionFileName"] = "Status.cs" });

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_pass_record_file()
    {
        // Arrange
        var source = """
            namespace MyApp.Models;

            public record UserDto(string Name, int Age);
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source, new Dictionary<string, string> { ["productionFileName"] = "UserDto.cs" });

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Should_throw_on_empty_or_whitespace_input(string input)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => new CSharpProjectStructureChecker().CheckAsync(input));
    }

    [Fact]
    public async Task Should_throw_on_null_input()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => new CSharpProjectStructureChecker().CheckAsync(null!));
    }

    [Fact]
    public async Task Should_enforce_block_scoped_namespace_when_editorconfig_says_block_scoped()
    {
        // Arrange — file-scoped namespace, should be flagged when block_scoped is preferred
        var source = """
            namespace MyApp.Services;

            public class UserService { }
            """;

        var data = new Dictionary<string, string> { ["csharp_style_namespace_declarations"] = "block_scoped" };

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source, data);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("File-scoped namespace", result);
            Assert.Contains("block_scoped", result);
        });
    }

    [Fact]
    public async Task Should_pass_block_scoped_namespace_when_editorconfig_says_block_scoped()
    {
        // Arrange — block-scoped namespace, matching the preference
        var source = """
            namespace MyApp.Services
            {
                public class UserService { }
            }
            """;

        var data = new Dictionary<string, string> { ["csharp_style_namespace_declarations"] = "block_scoped" };

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source, data);

        // Assert
        Assert.DoesNotContain("namespace", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_enforce_file_scoped_namespace_when_editorconfig_says_file_scoped()
    {
        // Arrange — block-scoped namespace
        var source = """
            namespace MyApp.Services
            {
                public class UserService { }
            }
            """;

        var data = new Dictionary<string, string> { ["csharp_style_namespace_declarations"] = "file_scoped" };

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source, data);

        // Assert — should still report block-scoped violation
        Assert.Contains("Block-scoped namespace", result);
    }

    [Fact]
    public async Task Should_enforce_file_scoped_namespace_when_no_editorconfig()
    {
        // Arrange — block-scoped namespace, no data
        var source = """
            namespace MyApp.Services
            {
                public class UserService { }
            }
            """;

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source);

        // Assert — default: enforce file-scoped
        Assert.Contains("Block-scoped namespace", result);
    }

    // -------------------------------------------------------------------------
    // Disabled mode (__disabled flag)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_skip_all_checks_when_disabled_and_no_editorconfig()
    {
        // Arrange — block-scoped namespace + #pragma (both violations) but tool disabled
        var source = """
            #pragma warning disable CA1234
            namespace MyApp
            {
                public class MyClass { }
                public class OtherClass { }
            }
            """;

        var data = new Dictionary<string, string> { ["__disabled"] = "true" };

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source, data);

        // Assert — nothing to enforce
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_run_namespace_check_when_disabled_but_ec_present()
    {
        // Arrange — file-scoped namespace, EC says block_scoped
        var source = """
            namespace MyApp.Services;

            public class UserService { }
            """;

        var data = new Dictionary<string, string>
        {
            ["__disabled"] = "true",
            ["csharp_style_namespace_declarations"] = "block_scoped",
        };

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source, data);

        // Assert — EC check still runs
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("File-scoped namespace", result);
        });
    }

    [Fact]
    public async Task Should_not_run_pragma_check_when_disabled_with_ec()
    {
        // Arrange — #pragma violation + EC for namespace
        var source = """
            #pragma warning disable CA1234
            namespace MyApp.Services;

            public class UserService { }
            """;

        var data = new Dictionary<string, string>
        {
            ["__disabled"] = "true",
            ["csharp_style_namespace_declarations"] = "file_scoped",
        };

        // Act
        var result = await new CSharpProjectStructureChecker().CheckAsync(source, data);

        // Assert — pragma is INST-only, skipped when disabled
        Assert.DoesNotContain("#pragma", result);
    }
}
