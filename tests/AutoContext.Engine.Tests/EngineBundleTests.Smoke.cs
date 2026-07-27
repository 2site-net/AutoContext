namespace AutoContext.Engine.Tests;

using System.Diagnostics;
using System.Text.Json;

using AutoContext.Engine.Tests.Support.Diagnostics;

[Trait("Category", "Smoke")]
public sealed class EngineBundleTests
{
    [Fact]
    public async Task Should_report_its_version_when_run_from_outside_the_bundle()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        EngineBundlePath.RequireStaged();

        // Act
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = EngineBundlePath.Executable,
            ArgumentList = { "--version" },
            WorkingDirectory = Path.GetTempPath(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(ct);
        var standardErrorTask = process.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(standardOutputTask, standardErrorTask);
        await process.WaitForExitAsync(ct);
        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        // Assert
        Assert.Multiple(
            () => Assert.True(
                process.ExitCode == 0,
                $"Expected the bundled engine to report its version from a working directory outside the bundle, "
                + $"but it exited with code {process.ExitCode}.{Environment.NewLine}{standardError}"),
            () => Assert.False(
                string.IsNullOrWhiteSpace(standardOutput),
                "Expected the bundled engine to write its version to stdout."));
    }

    [Fact]
    public void Should_stage_every_side_car_beside_the_binary()
    {
        // Arrange
        EngineBundlePath.RequireStaged();

        // Act + Assert
        Assert.Multiple(
            () => Assert.True(
                File.Exists(EngineBundlePath.Executable),
                $"Expected the engine executable at '{EngineBundlePath.Executable}'."),
            () => Assert.True(
                Directory.Exists(EngineBundlePath.Instructions),
                $"Expected the curated corpus at '{EngineBundlePath.Instructions}'."),
            () => Assert.NotEmpty(
                Directory.EnumerateFiles(EngineBundlePath.Instructions, "*.md", SearchOption.AllDirectories)),
            () => Assert.True(
                File.Exists(EngineBundlePath.WorkersManifest),
                $"Expected the worker roster at '{EngineBundlePath.WorkersManifest}'."));
    }

    [Fact]
    public void Should_stage_a_populated_directory_for_every_worker_in_the_roster()
    {
        // Arrange
        EngineBundlePath.RequireStaged();
        var roster = ReadWorkerIds(EngineBundlePath.WorkersManifest);

        // Act
        var stagedDirectories = roster
            .Select(id => Path.Combine(EngineBundlePath.Workers, id))
            .ToArray();

        // Assert
        Assert.Multiple(
            () => Assert.NotEmpty(roster),
            () => Assert.All(
                stagedDirectories,
                directory => Assert.True(
                    Directory.Exists(directory)
                        && Directory.EnumerateFileSystemEntries(directory).Any(),
                    $"Expected worker directory '{directory}' to be staged and non-empty.")));

        static string[] ReadWorkerIds(string manifestPath)
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(manifestPath));

            return [.. document.RootElement
                .GetProperty("workers")
                .EnumerateArray()
                .Select(static worker => worker.GetProperty("id").GetString()!)];
        }
    }
}
