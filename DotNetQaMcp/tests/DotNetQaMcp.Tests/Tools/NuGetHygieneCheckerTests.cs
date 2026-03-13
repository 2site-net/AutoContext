namespace DotNetQaMcp.Tests.Tools;

using DotNetQaMcp.Tools;

public sealed class NuGetHygieneCheckerTests
{
    [Fact]
    public void Should_pass_clean_project()
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
        var result = NuGetHygieneChecker.Check(csproj);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_reject_duplicate_package_reference()
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
        var result = NuGetHygieneChecker.Check(csproj);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("Duplicate PackageReference 'Moq'", result);
        });
    }

    [Fact]
    public void Should_not_flag_different_packages_as_duplicates()
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
        var result = NuGetHygieneChecker.Check(csproj);

        // Assert
        Assert.DoesNotContain("Duplicate", result);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("1.*")]
    [InlineData("[1.0,2.0)")]
    [InlineData("(,2.0]")]
    public void Should_reject_floating_or_range_version(string version)
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
        var result = NuGetHygieneChecker.Check(csproj);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("floating or range version", result);
            Assert.Contains("Pin to an exact version", result);
        });
    }

    [Fact]
    public void Should_pass_exact_version()
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
        var result = NuGetHygieneChecker.Check(csproj);

        // Assert
        Assert.DoesNotContain("floating", result);
    }

    [Fact]
    public void Should_reject_missing_version_without_cpm()
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
        var result = NuGetHygieneChecker.Check(csproj);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("no Version specified", result);
        });
    }

    [Fact]
    public void Should_pass_missing_version_with_cpm()
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
        var result = NuGetHygieneChecker.Check(csproj);

        // Assert
        Assert.DoesNotContain("no Version specified", result);
    }

    [Theory]
    [InlineData("Newtonsoft.Json", "System.Text.Json")]
    [InlineData("RestSharp", "System.Net.Http.HttpClient")]
    [InlineData("AutoMapper", "manual mapping or Mapster")]
    public void Should_flag_package_with_built_in_alternative(string package, string alternative)
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
        var result = NuGetHygieneChecker.Check(csproj);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains(alternative, result);
        });
    }

    [Fact]
    public void Should_not_flag_package_without_known_alternative()
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
        var result = NuGetHygieneChecker.Check(csproj);

        // Assert
        Assert.DoesNotContain("built-in", result);
    }

    [Fact]
    public void Should_report_multiple_violations()
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
        var result = NuGetHygieneChecker.Check(csproj);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("Duplicate", result);
            Assert.Contains("floating or range version", result);
            Assert.Contains("no Version specified", result);
            Assert.Contains("built-in .NET alternative", result);
        });
    }

    [Fact]
    public void Should_read_version_from_child_element()
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
        var result = NuGetHygieneChecker.Check(csproj);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_return_error_for_invalid_xml()
    {
        // Arrange
        var badXml = "<Project><ItemGroup><PackageReference";

        // Act
        var result = NuGetHygieneChecker.Check(badXml);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("Failed to parse", result);
        });
    }

    [Fact]
    public void Should_detect_duplicates_case_insensitively()
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
        var result = NuGetHygieneChecker.Check(csproj);

        // Assert
        Assert.Contains("Duplicate", result);
    }

    [Fact]
    public void Should_pass_empty_item_group()
    {
        // Arrange
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
              </ItemGroup>
            </Project>
            """;

        // Act
        var result = NuGetHygieneChecker.Check(csproj);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_handle_update_attribute()
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
        var result = NuGetHygieneChecker.Check(csproj);

        // Assert
        Assert.Contains("built-in .NET alternative", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Should_throw_on_empty_or_whitespace_input(string input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => NuGetHygieneChecker.Check(input));
    }

    [Fact]
    public void Should_throw_on_null_input()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => NuGetHygieneChecker.Check(null!));
    }
}
