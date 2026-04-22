namespace AutoContext.Mcp.Tools.Tests.Manifest;

using AutoContext.Mcp.Tools.Manifest;

public sealed class ManifestValidatorTests
{
    [Fact]
    public void Real_manifest_should_be_valid()
    {
        // Arrange
        var manifest = ManifestLoader.Parse(RealManifestFixture.Json);

        // Act
        var result = ManifestValidator.Validate(RealManifestFixture.Json, manifest);

        // Assert
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Should_detect_orphan_task_reference()
    {
        // Arrange
        const string json = """
            {
              "schemaVersion": "1",
              "dotnet": [{
                "tag": ".NET", "description": "x",
                "endpoint": "autocontext.dotnet-worker",
                "tools": [{
                  "tag": "NuGet", "description": "x",
                  "definition": {
                    "name": "analyze_nuget_references",
                    "description": "x",
                    "parameters": { "content": { "type": "string", "description": "x", "required": true } }
                  },
                  "tasks": ["does_not_exist"]
                }],
                "tasks": [
                  { "name": "analyze_nuget_hygiene", "version": "1.0.0" }
                ]
              }]
            }
            """;
        var manifest = ManifestLoader.Parse(json);

        // Act
        var result = ManifestValidator.Validate(json, manifest);

        // Assert
        Assert.Multiple(
            () => Assert.False(result.IsValid),
            () => Assert.Contains(result.Errors, e => e.Contains("references unknown task 'does_not_exist'", StringComparison.Ordinal)));
    }

    [Fact]
    public void Should_detect_unreferenced_task()
    {
        // Arrange
        const string json = """
            {
              "schemaVersion": "1",
              "dotnet": [{
                "tag": ".NET", "description": "x",
                "endpoint": "autocontext.dotnet-worker",
                "tools": [{
                  "tag": "NuGet", "description": "x",
                  "definition": {
                    "name": "analyze_nuget_references",
                    "description": "x",
                    "parameters": { "content": { "type": "string", "description": "x", "required": true } }
                  },
                  "tasks": ["analyze_nuget_hygiene"]
                }],
                "tasks": [
                  { "name": "analyze_nuget_hygiene", "version": "1.0.0" },
                  { "name": "orphaned_task", "version": "1.0.0" }
                ]
              }]
            }
            """;
        var manifest = ManifestLoader.Parse(json);

        // Act
        var result = ManifestValidator.Validate(json, manifest);

        // Assert
        Assert.Multiple(
            () => Assert.False(result.IsValid),
            () => Assert.Contains(result.Errors, e => e.Contains("'orphaned_task'", StringComparison.Ordinal) && e.Contains("not referenced", StringComparison.Ordinal)));
    }

    [Fact]
    public void Should_detect_duplicate_task_name_within_group()
    {
        // Arrange
        const string json = """
            {
              "schemaVersion": "1",
              "dotnet": [{
                "tag": ".NET", "description": "x",
                "endpoint": "autocontext.dotnet-worker",
                "tools": [{
                  "tag": "NuGet", "description": "x",
                  "definition": {
                    "name": "analyze_nuget_references",
                    "description": "x",
                    "parameters": { "content": { "type": "string", "description": "x", "required": true } }
                  },
                  "tasks": ["analyze_nuget_hygiene"]
                }],
                "tasks": [
                  { "name": "analyze_nuget_hygiene", "version": "1.0.0" },
                  { "name": "analyze_nuget_hygiene", "version": "2.0.0" }
                ]
              }]
            }
            """;
        var manifest = ManifestLoader.Parse(json);

        // Act
        var result = ManifestValidator.Validate(json, manifest);

        // Assert
        Assert.Contains(result.Errors, e => e.Contains("Duplicate task name 'analyze_nuget_hygiene'", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_detect_duplicate_tool_name_across_manifest()
    {
        // Arrange — same tool name in two different groups under different workers
        const string json = """
            {
              "schemaVersion": "1",
              "dotnet": [{
                "tag": ".NET", "description": "x",
                "endpoint": "autocontext.dotnet-worker",
                "tools": [{
                  "tag": "NuGet", "description": "x",
                  "definition": {
                    "name": "analyze_shared_name",
                    "description": "x",
                    "parameters": { "content": { "type": "string", "description": "x", "required": true } }
                  },
                  "tasks": ["task_a"]
                }],
                "tasks": [{ "name": "task_a", "version": "1.0.0" }]
              }],
              "web": [{
                "tag": "Web", "description": "x",
                "endpoint": "autocontext.web-worker",
                "tools": [{
                  "tag": "TS", "description": "x",
                  "definition": {
                    "name": "analyze_shared_name",
                    "description": "x",
                    "parameters": { "content": { "type": "string", "description": "x", "required": true } }
                  },
                  "tasks": ["task_b"]
                }],
                "tasks": [{ "name": "task_b", "version": "1.0.0" }]
              }]
            }
            """;
        var manifest = ManifestLoader.Parse(json);

        // Act
        var result = ManifestValidator.Validate(json, manifest);

        // Assert
        Assert.Contains(result.Errors, e => e.Contains("Duplicate tool name 'analyze_shared_name'", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_detect_schema_violation_on_bad_endpoint()
    {
        // Arrange — uppercase endpoint violates pattern ^[a-z0-9][a-z0-9.-]*$
        const string json = """
            {
              "schemaVersion": "1",
              "dotnet": [{
                "tag": ".NET", "description": "x",
                "endpoint": "AutoContext.Dotnet",
                "tools": [{
                  "tag": "NuGet", "description": "x",
                  "definition": {
                    "name": "analyze_nuget_references",
                    "description": "x",
                    "parameters": { "content": { "type": "string", "description": "x", "required": true } }
                  },
                  "tasks": ["analyze_nuget_hygiene"]
                }],
                "tasks": [{ "name": "analyze_nuget_hygiene", "version": "1.0.0" }]
              }]
            }
            """;
        var manifest = ManifestLoader.Parse(json);

        // Act
        var result = ManifestValidator.Validate(json, manifest);

        // Assert
        Assert.Multiple(
            () => Assert.False(result.IsValid),
            () => Assert.Contains(result.Errors, e => e.StartsWith("Schema error", StringComparison.Ordinal)));
    }

    [Fact]
    public void Should_throw_ArgumentNullException_for_null_inputs()
    {
        var manifest = ManifestLoader.Parse(RealManifestFixture.Json);

        Assert.Multiple(
            () => Assert.Throws<ArgumentNullException>(() => ManifestValidator.Validate(null!, manifest)),
            () => Assert.Throws<ArgumentNullException>(() => ManifestValidator.Validate(RealManifestFixture.Json, null!)));
    }
}
