namespace AutoContext.Worker.DotNet.Tests.Tasks.NuGet;

using System.Text.Json;

using AutoContext.Worker.DotNet.Tasks.NuGet;
using AutoContext.Worker.Testing;

public sealed class NuGetHygieneTaskTests
{
    [Fact]
    public void Should_expose_task_name()
    {
        // Arrange
        var sut = new NuGetHygieneTask();

        // Act & Assert
        Assert.Equal("check_nuget_hygiene", sut.TaskName);
    }

    [Fact]
    public async Task Should_pass_clean_project()
    {
        // Arrange
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="xunit" Version="2.9.3" />
              </ItemGroup>
            </Project>
            """;

        // Act
        var (passed, report) = await RunAsync(csproj);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.True(passed);
            Assert.StartsWith("✅", report);
        });
    }

    [Fact]
    public async Task Should_reject_duplicate_package_reference()
    {
        // Arrange
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Moq" Version="4.20.0" />
                <PackageReference Include="Moq" Version="4.18.0" />
              </ItemGroup>
            </Project>
            """;

        // Act
        var (passed, report) = await RunAsync(csproj);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.False(passed);
            Assert.StartsWith("❌", report);
            Assert.Contains("Duplicate PackageReference 'Moq'", report);
        });
    }

    [Fact]
    public async Task Should_not_flag_different_packages_as_duplicates()
    {
        // Arrange
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Moq" Version="4.20.0" />
                <PackageReference Include="xunit" Version="2.9.3" />
              </ItemGroup>
            </Project>
            """;

        // Act
        var (_, report) = await RunAsync(csproj);

        // Assert
        Assert.DoesNotContain("Duplicate", report);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("1.*")]
    [InlineData("[1.0,2.0)")]
    [InlineData("(,2.0]")]
    public async Task Should_reject_floating_or_range_version(string version)
    {
        // Arrange
        var csproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="SomePackage" Version="{version}" />
              </ItemGroup>
            </Project>
            """;

        // Act
        var (passed, report) = await RunAsync(csproj);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.False(passed);
            Assert.StartsWith("❌", report);
            Assert.Contains("floating or range version", report);
            Assert.Contains("Pin to an exact version", report);
        });
    }

    [Fact]
    public async Task Should_pass_exact_version()
    {
        // Arrange
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="SomePackage" Version="3.1.4" />
              </ItemGroup>
            </Project>
            """;

        // Act
        var (_, report) = await RunAsync(csproj);

        // Assert
        Assert.DoesNotContain("floating", report);
    }

    [Fact]
    public async Task Should_reject_missing_version_without_cpm()
    {
        // Arrange
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="SomePackage" />
              </ItemGroup>
            </Project>
            """;

        // Act
        var (passed, report) = await RunAsync(csproj);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.False(passed);
            Assert.StartsWith("❌", report);
            Assert.Contains("no Version specified", report);
        });
    }

    [Fact]
    public async Task Should_pass_missing_version_with_cpm()
    {
        // Arrange
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="SomePackage" />
              </ItemGroup>
            </Project>
            """;

        // Act
        var (_, report) = await RunAsync(csproj);

        // Assert
        Assert.DoesNotContain("no Version specified", report);
    }

    [Theory]
    [InlineData("Newtonsoft.Json", "System.Text.Json")]
    [InlineData("RestSharp", "System.Net.Http.HttpClient")]
    [InlineData("AutoMapper", "manual mapping or Mapster")]
    public async Task Should_flag_package_with_built_in_alternative(string package, string alternative)
    {
        // Arrange
        var csproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="{package}" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """;

        // Act
        var (passed, report) = await RunAsync(csproj);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.False(passed);
            Assert.StartsWith("❌", report);
            Assert.Contains(alternative, report);
        });
    }

    [Fact]
    public async Task Should_not_flag_package_without_known_alternative()
    {
        // Arrange
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Serilog" Version="4.0.0" />
              </ItemGroup>
            </Project>
            """;

        // Act
        var (_, report) = await RunAsync(csproj);

        // Assert
        Assert.DoesNotContain("built-in", report);
    }

    [Fact]
    public async Task Should_report_multiple_violations()
    {
        // Arrange
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="*" />
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
                <PackageReference Include="OrphanPackage" />
              </ItemGroup>
            </Project>
            """;

        // Act
        var (passed, report) = await RunAsync(csproj);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.False(passed);
            Assert.StartsWith("❌", report);
            Assert.Contains("Duplicate", report);
            Assert.Contains("floating or range version", report);
            Assert.Contains("no Version specified", report);
            Assert.Contains("built-in .NET alternative", report);
        });
    }

    [Fact]
    public async Task Should_read_version_from_child_element()
    {
        // Arrange
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="SomePackage">
                  <Version>2.0.0</Version>
                </PackageReference>
              </ItemGroup>
            </Project>
            """;

        // Act
        var (passed, report) = await RunAsync(csproj);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.True(passed);
            Assert.StartsWith("✅", report);
        });
    }

    [Fact]
    public async Task Should_return_error_for_invalid_xml()
    {
        // Arrange
        var badXml = "<Project><ItemGroup><PackageReference";

        // Act
        var (passed, report) = await RunAsync(badXml);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.False(passed);
            Assert.StartsWith("❌", report);
            Assert.Contains("Failed to parse", report);
        });
    }

    [Fact]
    public async Task Should_detect_duplicates_case_insensitively()
    {
        // Arrange
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="moq" Version="4.20.0" />
                <PackageReference Include="Moq" Version="4.18.0" />
              </ItemGroup>
            </Project>
            """;

        // Act
        var (_, report) = await RunAsync(csproj);

        // Assert
        Assert.Contains("Duplicate", report);
    }

    [Fact]
    public async Task Should_pass_empty_item_group()
    {
        // Arrange
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
              </ItemGroup>
            </Project>
            """;

        // Act
        var (passed, report) = await RunAsync(csproj);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.True(passed);
            Assert.StartsWith("✅", report);
        });
    }

    [Fact]
    public async Task Should_handle_update_attribute()
    {
        // Arrange
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Update="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;

        // Act
        var (_, report) = await RunAsync(csproj);

        // Assert
        Assert.Contains("built-in .NET alternative", report);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Should_throw_on_empty_or_whitespace_content(string input)
    {
        // Arrange
        var sut = new NuGetHygieneTask();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(new { content = input }));
    }

    [Fact]
    public async Task Should_throw_when_content_property_missing()
    {
        // Arrange
        var sut = new NuGetHygieneTask();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(new { }));
    }

    [Fact]
    public async Task Should_throw_when_content_property_not_a_string()
    {
        // Arrange
        var sut = new NuGetHygieneTask();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(new { content = 42 }));
    }

    private static async Task<(bool Passed, string Report)> RunAsync(string content)
    {
        var sut = new NuGetHygieneTask();
        var output = await sut.ExecuteAsync(new { content });

        var passed = output.GetProperty("passed").GetBoolean();
        var report = output.GetProperty("report").GetString()!;

        return (passed, report);
    }
}
