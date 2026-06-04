namespace AutoContext.Engine.Core.Tests.Workspace.Context;

using AutoContext.Engine.Core.Tests.Support.Workspace.Context;
using AutoContext.Engine.Core.Workspace.Context;
using AutoContext.Engine.Tests.Support.IO;

using Microsoft.Extensions.Logging.Abstractions;

public sealed partial class WorkspaceContextDetectorTests
{
    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_invalid_arguments()
        {
            // Arrange
            var rules = WorkspaceDetectionRules.FileRules;
            var scans = WorkspaceDetectionRules.ContentScans;
            var edges = WorkspaceDetectionRules.FlagActivationEdges;
            var time = TimeProvider.System;
            var debounce = TimeSpan.FromMilliseconds(500);
            var logger = NullLogger<WorkspaceContextDetector>.Instance;

            // Act + Assert
            Assert.Multiple(
                () => Assert.Throws<ArgumentNullException>(
                    () => new WorkspaceContextDetector(null!, rules, scans, edges, time, debounce, logger)),
                () => Assert.Throws<ArgumentException>(
                    () => new WorkspaceContextDetector(
                        new FakeWorkspaceEngineInfo { WorkspacePath = "  " }, rules, scans, edges, time, debounce, logger)),
                () => Assert.Throws<ArgumentNullException>(
                    () => new WorkspaceContextDetector(
                        new FakeWorkspaceEngineInfo { WorkspacePath = "x" }, null!, scans, edges, time, debounce, logger)),
                () => Assert.Throws<ArgumentNullException>(
                    () => new WorkspaceContextDetector(
                        new FakeWorkspaceEngineInfo { WorkspacePath = "x" }, rules, null!, edges, time, debounce, logger)),
                () => Assert.Throws<ArgumentNullException>(
                    () => new WorkspaceContextDetector(
                        new FakeWorkspaceEngineInfo { WorkspacePath = "x" }, rules, scans, null!, time, debounce, logger)),
                () => Assert.Throws<ArgumentOutOfRangeException>(
                    () => new WorkspaceContextDetector(
                        new FakeWorkspaceEngineInfo { WorkspacePath = "x" },
                        rules, scans, edges, time, TimeSpan.Zero, logger)));
        }
    }

    public sealed class DetectAsync(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_return_empty_for_empty_workspace()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(result.Flags);
        }

        [Fact]
        public async Task Should_expose_workspace_info_metadata_and_bump_revision_when_snapshot_changes()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "App.csproj");
            var instanceId = Guid.NewGuid();

            using var sut = new WorkspaceContextDetector(
                new FakeWorkspaceEngineInfo
                {
                    WorkspacePath = root,
                    InstanceId = instanceId,
                    InstanceLabel = "primary",
                    IdleTimeout = TimeSpan.FromSeconds(15),
                },
                WorkspaceDetectionRules.FileRules,
                WorkspaceDetectionRules.ContentScans,
                WorkspaceDetectionRules.FlagActivationEdges,
                TimeProvider.System,
                WorkspaceContextDetector.DefaultDebounceDelay,
                NullLogger<WorkspaceContextDetector>.Instance);

            // Act
            _ = await sut.DetectAsync(TestContext.Current.CancellationToken);
            WorkspaceFileTestWriter.Write(root, "app.py");
            var second = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(instanceId, sut.EngineInfo.InstanceId),
                () => Assert.Equal("primary", sut.EngineInfo.InstanceLabel),
                () => Assert.Equal(TimeSpan.FromSeconds(15), sut.EngineInfo.IdleTimeout),
                () => Assert.Equal(2L, sut.Revision),
                () => Assert.True(second.Has("hasCSharp")),
                () => Assert.True(second.Has("hasPython")));
        }

        [Theory]
        [InlineData("App.slnx", "hasDotNet")]
        [InlineData("App.csproj", "hasCSharp")]
        [InlineData("Program.cs", "hasCSharp")]
        [InlineData("Lib.fsproj", "hasFSharp")]
        [InlineData("Program.fs", "hasFSharp")]
        [InlineData("Lib.vbproj", "hasVbNet")]
        [InlineData("Program.vb", "hasVbNet")]
        [InlineData("Counter.razor", "hasBlazor")]
        [InlineData("Window.xaml", "hasXaml")]
        [InlineData("Page.aspx", "hasWebForms")]
        [InlineData("Index.cshtml", "hasRazor")]
        [InlineData("index.html", "hasHtml")]
        [InlineData("styles.css", "hasCss")]
        [InlineData("main.dart", "hasDart")]
        [InlineData("app.js", "hasJavaScript")]
        [InlineData("app.ts", "hasTypeScript")]
        [InlineData("script.ps1", "hasPowerShell")]
        [InlineData("deploy.sh", "hasBash")]
        [InlineData("run.bat", "hasBatch")]
        [InlineData("config.yml", "hasYaml")]
        [InlineData("Main.java", "hasJava")]
        [InlineData("Main.kt", "hasKotlin")]
        [InlineData("Main.scala", "hasScala")]
        [InlineData("Build.groovy", "hasGroovy")]
        [InlineData("main.c", "hasC")]
        [InlineData("main.cpp", "hasCpp")]
        [InlineData("app.rb", "hasRuby")]
        [InlineData("main.rs", "hasRust")]
        [InlineData("main.swift", "hasSwift")]
        [InlineData("main.go", "hasGo")]
        [InlineData("app.py", "hasPython")]
        [InlineData("main.lua", "hasLua")]
        [InlineData("index.php", "hasPhp")]
        public async Task Should_detect_flag_from_extension(string relativePath, string expectedFlag)
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, relativePath);
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.Has(expectedFlag));
        }

        [Theory]
        [InlineData("Cargo.toml", "hasRust")]
        [InlineData("Gemfile", "hasRuby")]
        [InlineData("go.mod", "hasGo")]
        [InlineData("composer.json", "hasPhp")]
        [InlineData("pom.xml", "hasJava")]
        [InlineData("build.gradle", "hasJava")]
        [InlineData("build.sbt", "hasScala")]
        [InlineData("Package.swift", "hasSwift")]
        [InlineData("pyproject.toml", "hasPython")]
        [InlineData("pubspec.yaml", "hasDart")]
        public async Task Should_detect_flag_from_file_name(string relativePath, string expectedFlag)
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, relativePath);
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.Has(expectedFlag));
        }

        [Fact]
        public async Task Should_set_hasNodeJs_for_package_json()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "package.json", "{}");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.True(result.Has("hasNodeJs")),
                () => Assert.True(result.Has("hasJavaScript")));
        }

        [Theory]
        [InlineData("app.cs", "hasCSharp")]
        [InlineData("app.fs", "hasFSharp")]
        [InlineData("app.vb", "hasVbNet")]
        public async Task Should_set_language_flag_but_not_hasDotNet_for_a_bare_source_file(
            string relativePath,
            string expectedFlag)
        {
            // Arrange — a bare .NET source file (e.g. dotnet run app.cs,
            // no project) is source for its language but not a .NET
            // project structure: the language flag rises while hasDotNet
            // stays project-scoped.
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, relativePath);
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.True(result.Has(expectedFlag)),
                () => Assert.False(result.Has("hasDotNet")));
        }

        [Fact]
        public async Task Should_detect_docker_from_glob_pattern()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "Dockerfile");
            WorkspaceFileTestWriter.Write(root, "deploy/Dockerfile.production");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.Has("hasDocker"));
        }

        [Fact]
        public async Task Should_detect_unity_from_nested_glob_pattern()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "ProjectSettings/ProjectSettings.asset");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.True(result.Has("hasUnity")),
                () => Assert.True(result.Has("hasCSharp")));
        }

        [Fact]
        public async Task Should_set_hasGit_when_git_directory_present()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            Directory.CreateDirectory(Path.Combine(root, ".git"));
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.Has("hasGit"));
        }

        [Theory]
        [InlineData("react", "hasReact")]
        [InlineData("@angular/core", "hasAngular")]
        [InlineData("vue", "hasVue")]
        [InlineData("svelte", "hasSvelte")]
        [InlineData("vitest", "hasVitest")]
        [InlineData("jest", "hasJest")]
        [InlineData("jasmine", "hasJasmine")]
        [InlineData("mocha", "hasMocha")]
        [InlineData("@playwright/test", "hasPlaywright")]
        [InlineData("cypress", "hasCypress")]
        [InlineData("next", "hasNextJs")]
        [InlineData("graphql", "hasGraphql")]
        public async Task Should_detect_flag_from_package_json_dependency(string dependency, string expectedFlag)
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(
                root,
                "package.json",
                $$"""{ "dependencies": { "{{dependency}}": "^1.0.0" } }""");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.Has(expectedFlag));
        }

        [Theory]
        [InlineData("<Project Sdk=\"Microsoft.NET.Sdk.Web\" />", "hasAspNetCore")]
        [InlineData("<PackageReference Include=\"Dapper\" />", "hasDapper")]
        [InlineData("<PackageReference Include=\"Microsoft.EntityFrameworkCore\" />", "hasEntityFrameworkCore")]
        [InlineData("<UseMaui>true</UseMaui>", "hasMaui")]
        [InlineData("<PackageReference Include=\"MongoDB.Driver\" />", "hasMongoDb")]
        [InlineData("<PackageReference Include=\"xunit\" />", "hasXunit")]
        [InlineData("<PackageReference Include=\"MSTest.TestFramework\" />", "hasMsTest")]
        [InlineData("<PackageReference Include=\"NUnit\" />", "hasNUnit")]
        [InlineData("<UseWPF>true</UseWPF>", "hasWpf")]
        [InlineData("<UseWindowsForms>true</UseWindowsForms>", "hasWinForms")]
        [InlineData("<PackageReference Include=\"MySqlConnector\" />", "hasMySql")]
        [InlineData("<PackageReference Include=\"Oracle.ManagedDataAccess\" />", "hasOracle")]
        [InlineData("<PackageReference Include=\"Npgsql\" />", "hasPostgres")]
        [InlineData("<PackageReference Include=\"Microsoft.Data.Sqlite\" />", "hasSqlite")]
        [InlineData("<PackageReference Include=\"Microsoft.Data.SqlClient\" />", "hasSqlServer")]
        [InlineData("<PackageReference Include=\"Grpc.AspNetCore\" />", "hasGrpc")]
        [InlineData("<PackageReference Include=\"MediatR\" />", "hasMediatR")]
        [InlineData("<PackageReference Include=\"StackExchange.Redis\" />", "hasRedis")]
        [InlineData("<PackageReference Include=\"Microsoft.AspNetCore.SignalR\" />", "hasSignalR")]
        [InlineData("<PackageReference Include=\"HotChocolate\" />", "hasGraphql")]
        public async Task Should_detect_flag_from_project_file_content(string projectContent, string expectedFlag)
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "App.csproj", projectContent);
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.Has(expectedFlag));
        }

        [Fact]
        public async Task Should_cascade_typescript_to_javascript()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "app.ts");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            string[] expected = ["hasJavaScript", "hasTypeScript"];
            Assert.Equal(expected, result.Flags.Order(StringComparer.Ordinal));
        }

        [Fact]
        public async Task Should_cascade_nextjs_through_react_to_nodejs()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "package.json", """{ "dependencies": { "next": "^14.0.0" } }""");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            string[] expected = ["hasJavaScript", "hasNextJs", "hasNodeJs", "hasReact"];
            Assert.Equal(expected, result.Flags.Order(StringComparer.Ordinal));
        }

        [Fact]
        public async Task Should_cascade_angular_to_typescript_javascript_and_nodejs()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(
                root,
                "package.json",
                """{ "dependencies": { "@angular/core": "^17.0.0" } }""");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            string[] expected = ["hasAngular", "hasJavaScript", "hasNodeJs", "hasTypeScript"];
            Assert.Equal(expected, result.Flags.Order(StringComparer.Ordinal));
        }

        [Fact]
        public async Task Should_cascade_blazor_to_aspnetcore_csharp_and_web_flags()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "Counter.razor");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            string[] expected =
            [
                "hasAspNetCore", "hasBlazor", "hasCSharp", "hasCss",
                "hasDotNet", "hasHtml", "hasRazor",
            ];
            Assert.Equal(expected, result.Flags.Order(StringComparer.Ordinal));
        }

        [Fact]
        public async Task Should_cascade_signalr_to_aspnetcore_and_dotnet()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(
                root,
                "App.csproj",
                "<PackageReference Include=\"Microsoft.AspNetCore.SignalR\" />");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            string[] expected = ["hasAspNetCore", "hasCSharp", "hasDotNet", "hasRazor", "hasSignalR"];
            Assert.Equal(expected, result.Flags.Order(StringComparer.Ordinal));
        }

        [Fact]
        public async Task Should_cascade_wpf_to_xaml_and_dotnet()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "App.csproj", "<UseWPF>true</UseWPF>");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            string[] expected = ["hasCSharp", "hasDotNet", "hasWpf", "hasXaml"];
            Assert.Equal(expected, result.Flags.Order(StringComparer.Ordinal));
        }

        [Fact]
        public async Task Should_cascade_html_to_css()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "index.html");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            string[] expected = ["hasCss", "hasHtml"];
            Assert.Equal(expected, result.Flags.Order(StringComparer.Ordinal));
        }

        [Theory]
        [InlineData("Main.java", "hasJava")]
        [InlineData("Main.kt", "hasKotlin")]
        [InlineData("Main.scala", "hasScala")]
        [InlineData("Build.groovy", "hasGroovy")]
        public async Task Should_cascade_jvm_language_to_jvm(string relativePath, string languageFlag)
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, relativePath);
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.True(result.Has(languageFlag)),
                () => Assert.True(result.Has("hasJvm")));
        }

        [Theory]
        [InlineData("main.c", "hasC")]
        [InlineData("main.cpp", "hasCpp")]
        [InlineData("main.rs", "hasRust")]
        [InlineData("main.go", "hasGo")]
        public async Task Should_cascade_native_language_to_native(string relativePath, string languageFlag)
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, relativePath);
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.True(result.Has(languageFlag)),
                () => Assert.True(result.Has("hasNative")));
        }

        [Theory]
        [InlineData("<PackageReference Include=\"xunit\" />", "hasXunit")]
        [InlineData("<PackageReference Include=\"MSTest.TestFramework\" />", "hasMsTest")]
        [InlineData("<PackageReference Include=\"NUnit\" />", "hasNUnit")]
        public async Task Should_cascade_dotnet_test_framework_to_dotnet_testing(string projectContent, string frameworkFlag)
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "Tests.csproj", projectContent);
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.True(result.Has(frameworkFlag)),
                () => Assert.True(result.Has("hasDotNetTesting")),
                () => Assert.True(result.Has("hasDotNet")));
        }

        [Theory]
        [InlineData("vitest", "hasVitest")]
        [InlineData("jest", "hasJest")]
        [InlineData("jasmine", "hasJasmine")]
        [InlineData("mocha", "hasMocha")]
        [InlineData("@playwright/test", "hasPlaywright")]
        [InlineData("cypress", "hasCypress")]
        public async Task Should_cascade_web_test_framework_to_web_testing(string dependency, string frameworkFlag)
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(
                root,
                "package.json",
                $$"""{ "devDependencies": { "{{dependency}}": "^1.0.0" } }""");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.True(result.Has(frameworkFlag)),
                () => Assert.True(result.Has("hasWebTesting")),
                () => Assert.True(result.Has("hasNodeJs")));
        }

        [Fact]
        public async Task Should_not_raise_framework_flags_when_package_json_has_no_known_dependencies()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(
                root,
                "package.json",
                """{ "dependencies": { "lodash": "^4.0.0" } }""");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            string[] expected = ["hasJavaScript", "hasNodeJs"];
            Assert.Equal(expected, result.Flags.Order(StringComparer.Ordinal));
        }

        [Fact]
        public async Task Should_not_raise_content_flags_when_project_file_has_no_known_references()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            string[] expected = ["hasCSharp", "hasDotNet"];
            Assert.Equal(expected, result.Flags.Order(StringComparer.Ordinal));
        }

        [Fact]
        public async Task Should_isolate_unrelated_language_flags()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "app.py");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            string[] expected = ["hasPython"];
            Assert.Equal(expected, result.Flags.Order(StringComparer.Ordinal));
        }

        [Fact]
        public async Task Should_never_raise_a_flag_outside_the_rule_table_universe()
        {
            // Arrange
            var universe = new HashSet<string>(StringComparer.Ordinal) { "hasGit", "hasNodeJs" };
            foreach (var rule in WorkspaceDetectionRules.FileRules)
            {
                universe.Add(rule.Flag);
            }

            foreach (var rule in WorkspaceDetectionRules.ContentScans.SelectMany(scan => scan.Rules))
            {
                universe.Add(rule.Flag);
            }

            foreach (var edge in WorkspaceDetectionRules.FlagActivationEdges)
            {
                universe.Add(edge.Child);
                universe.Add(edge.Parent);
            }

            var root = tempDirectory.CreateDirectory();
            Directory.CreateDirectory(Path.Combine(root, ".git"));
            WorkspaceFileTestWriter.Write(root, "Dockerfile");
            WorkspaceFileTestWriter.Write(root, "ProjectSettings/ProjectSettings.asset");
            WorkspaceFileTestWriter.Write(root, "app.ts");
            WorkspaceFileTestWriter.Write(root, "Main.kt");
            WorkspaceFileTestWriter.Write(root, "main.rs");
            WorkspaceFileTestWriter.Write(
                root,
                "App.csproj",
                """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup><UseWPF>true</UseWPF><UseWindowsForms>true</UseWindowsForms><UseMaui>true</UseMaui></PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Dapper" />
                    <PackageReference Include="Microsoft.EntityFrameworkCore" />
                    <PackageReference Include="MongoDB.Driver" />
                    <PackageReference Include="xunit" />
                    <PackageReference Include="MSTest.TestFramework" />
                    <PackageReference Include="NUnit" />
                    <PackageReference Include="MySqlConnector" />
                    <PackageReference Include="Oracle.ManagedDataAccess" />
                    <PackageReference Include="Npgsql" />
                    <PackageReference Include="Microsoft.Data.Sqlite" />
                    <PackageReference Include="Microsoft.Data.SqlClient" />
                    <PackageReference Include="Grpc.AspNetCore" />
                    <PackageReference Include="MediatR" />
                    <PackageReference Include="StackExchange.Redis" />
                    <PackageReference Include="Microsoft.AspNetCore.SignalR" />
                    <PackageReference Include="HotChocolate" />
                  </ItemGroup>
                </Project>
                """);
            WorkspaceFileTestWriter.Write(
                root,
                "package.json",
                """
                {
                  "dependencies": { "react": "^1", "@angular/core": "^1", "vue": "^1", "svelte": "^1", "next": "^1", "graphql": "^1" },
                  "devDependencies": { "vitest": "^1", "jest": "^1", "jasmine": "^1", "mocha": "^1", "@playwright/test": "^1", "cypress": "^1" }
                }
                """);
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            var stray = result.Flags.Where(flag => !universe.Contains(flag)).Order(StringComparer.Ordinal);
            Assert.Multiple(
                () => Assert.NotEmpty(result.Flags),
                () => Assert.Empty(stray));
        }

        [Fact]
        public async Task Should_ignore_files_in_excluded_directories()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "node_modules/pkg/Library.csproj");
            WorkspaceFileTestWriter.Write(root, "obj/Generated.cs");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(result.Flags);
        }
    }
}
