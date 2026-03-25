namespace SharpPilot.Tests.Tools.DotNet;

using System.Text.Json.Nodes;

using SharpPilot.Tools.DotNet;

public sealed class CSharpProjectStructureCheckerTests
{
    [Fact]
    public void Should_pass_well_structured_file()
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
        var result = new CSharpProjectStructureChecker().Check(source, new JsonObject { ["productionFileName"] = "UserService.cs" });

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_pass_without_file_name()
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
        var result = new CSharpProjectStructureChecker().Check(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_reject_block_scoped_namespace()
    {
        // Arrange
        var source = """
            namespace MyApp.Services
            {
                public class UserService { }
            }
            """;

        // Act
        var result = new CSharpProjectStructureChecker().Check(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("Block-scoped namespace", result);
            Assert.Contains("file-scoped namespace", result);
        });
    }

    [Fact]
    public void Should_pass_file_scoped_namespace()
    {
        // Arrange
        var source = """
            namespace MyApp.Services;

            public class UserService { }
            """;

        // Act
        var result = new CSharpProjectStructureChecker().Check(source);

        // Assert
        Assert.DoesNotContain("namespace", result);
    }

    [Fact]
    public void Should_reject_multiple_types_in_one_file()
    {
        // Arrange
        var source = """
            namespace MyApp.Models;

            public class User { }

            public class UserDto { }
            """;

        // Act
        var result = new CSharpProjectStructureChecker().Check(source);

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
    public void Should_reject_type_and_delegate_in_same_file()
    {
        // Arrange
        var source = """
            namespace MyApp.Models;

            public delegate void MyCallback(int result);

            public class EventProcessor { }
            """;

        // Act
        var result = new CSharpProjectStructureChecker().Check(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("2 top-level type declarations", result);
        });
    }

    [Fact]
    public void Should_pass_single_type_with_nested_type()
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
        var result = new CSharpProjectStructureChecker().Check(source);

        // Assert
        Assert.DoesNotContain("top-level type", result);
    }

    [Fact]
    public void Should_reject_file_name_mismatch()
    {
        // Arrange
        var source = """
            namespace MyApp.Services;

            public class UserService { }
            """;

        // Act
        var result = new CSharpProjectStructureChecker().Check(source, new JsonObject { ["productionFileName"] = "WrongName.cs" });

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
    public void Should_pass_file_name_matching_type()
    {
        // Arrange
        var source = """
            namespace MyApp.Services;

            public class UserService { }
            """;

        // Act
        var result = new CSharpProjectStructureChecker().Check(source, new JsonObject { ["productionFileName"] = "UserService.cs" });

        // Assert
        Assert.DoesNotContain("file name", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Should_reject_pragma_warning_disable()
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
        var result = new CSharpProjectStructureChecker().Check(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("#pragma warning disable", result);
            Assert.Contains("[SuppressMessage]", result);
        });
    }

    [Fact]
    public void Should_pass_pragma_warning_restore()
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
        var result = new CSharpProjectStructureChecker().Check(source);

        // Assert
        Assert.DoesNotContain("#pragma", result);
    }

    [Fact]
    public void Should_report_multiple_violations()
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
        var result = new CSharpProjectStructureChecker().Check(source);

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
    public void Should_not_check_file_name_when_multiple_types()
    {
        // Arrange
        var source = """
            namespace MyApp.Models;

            public class User { }

            public class UserDto { }
            """;

        // Act
        var result = new CSharpProjectStructureChecker().Check(source, new JsonObject { ["productionFileName"] = "User.cs" });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.Contains("top-level type declarations", result);
            Assert.DoesNotContain("file name", result, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Should_pass_single_delegate_file()
    {
        // Arrange
        var source = """
            namespace MyApp.Models;

            public delegate void DataCallback(int result);
            """;

        // Act
        var result = new CSharpProjectStructureChecker().Check(source, new JsonObject { ["productionFileName"] = "DataCallback.cs" });

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_reject_delegate_file_name_mismatch()
    {
        // Arrange
        var source = """
            namespace MyApp.Models;

            public delegate void DataCallback(int result);
            """;

        // Act
        var result = new CSharpProjectStructureChecker().Check(source, new JsonObject { ["productionFileName"] = "Wrong.cs" });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("'DataCallback'", result);
        });
    }

    [Fact]
    public void Should_pass_interface_file()
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
        var result = new CSharpProjectStructureChecker().Check(source, new JsonObject { ["productionFileName"] = "IUserRepository.cs" });

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_pass_enum_file()
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
        var result = new CSharpProjectStructureChecker().Check(source, new JsonObject { ["productionFileName"] = "Status.cs" });

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_pass_record_file()
    {
        // Arrange
        var source = """
            namespace MyApp.Models;

            public record UserDto(string Name, int Age);
            """;

        // Act
        var result = new CSharpProjectStructureChecker().Check(source, new JsonObject { ["productionFileName"] = "UserDto.cs" });

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Should_throw_on_empty_or_whitespace_input(string input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new CSharpProjectStructureChecker().Check(input));
    }

    [Fact]
    public void Should_throw_on_null_input()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CSharpProjectStructureChecker().Check(null!));
    }

    [Fact]
    public void Should_enforce_block_scoped_namespace_when_editorconfig_says_block_scoped()
    {
        // Arrange — file-scoped namespace, should be flagged when block_scoped is preferred
        var source = """
            namespace MyApp.Services;

            public class UserService { }
            """;

        var data = new JsonObject { ["csharp_style_namespace_declarations"] = "block_scoped" };

        // Act
        var result = new CSharpProjectStructureChecker().Check(source, data);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("File-scoped namespace", result);
            Assert.Contains("block_scoped", result);
        });
    }

    [Fact]
    public void Should_pass_block_scoped_namespace_when_editorconfig_says_block_scoped()
    {
        // Arrange — block-scoped namespace, matching the preference
        var source = """
            namespace MyApp.Services
            {
                public class UserService { }
            }
            """;

        var data = new JsonObject { ["csharp_style_namespace_declarations"] = "block_scoped" };

        // Act
        var result = new CSharpProjectStructureChecker().Check(source, data);

        // Assert
        Assert.DoesNotContain("namespace", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Should_enforce_file_scoped_namespace_when_editorconfig_says_file_scoped()
    {
        // Arrange — block-scoped namespace
        var source = """
            namespace MyApp.Services
            {
                public class UserService { }
            }
            """;

        var data = new JsonObject { ["csharp_style_namespace_declarations"] = "file_scoped" };

        // Act
        var result = new CSharpProjectStructureChecker().Check(source, data);

        // Assert — should still report block-scoped violation
        Assert.Contains("Block-scoped namespace", result);
    }

    [Fact]
    public void Should_enforce_file_scoped_namespace_when_no_editorconfig()
    {
        // Arrange — block-scoped namespace, no data
        var source = """
            namespace MyApp.Services
            {
                public class UserService { }
            }
            """;

        // Act
        var result = new CSharpProjectStructureChecker().Check(source);

        // Assert — default: enforce file-scoped
        Assert.Contains("Block-scoped namespace", result);
    }
}
