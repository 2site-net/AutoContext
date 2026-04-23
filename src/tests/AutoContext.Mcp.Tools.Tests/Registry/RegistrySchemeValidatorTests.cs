namespace AutoContext.Mcp.Tools.Tests.Registry;

using AutoContext.Mcp.Tools.Registry;

public sealed class RegistrySchemeValidatorTests
{
    [Fact]
    public void Real_registry_should_be_valid()
    {
        // Arrange
        var registry = RegistryLoader.Parse(RegistryEmbeddedResourceLoader.Json);

        // Act
        var result = RegistrySchemeValidator.Validate(RegistryEmbeddedResourceLoader.Json, registry);

        // Assert
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Should_detect_duplicate_task_name_within_tool()
    {
        // Arrange
        const string json = """
            {
              "schemaVersion": "1",
              "workers": [{
                "name": "AutoContext.Worker.DotNet",
                "endpoint": "autocontext.worker-dotnet",
                "tools": [{
                  "name": "analyze_nuget_references",
                  "description": "x",
                  "parameters": { "content": { "type": "string", "description": "x", "required": true } },
                  "tasks": [
                    { "name": "analyze_nuget_hygiene" },
                    { "name": "analyze_nuget_hygiene" }
                  ]
                }]
              }]
            }
            """;
        var registry = RegistryLoader.Parse(json);

        // Act
        var result = RegistrySchemeValidator.Validate(json, registry);

        // Assert
        Assert.Contains(result.Errors, e => e.Contains("Duplicate task name 'analyze_nuget_hygiene'", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_detect_duplicate_tool_name_across_registry()
    {
        // Arrange — same tool name in two different workers
        const string json = """
            {
              "schemaVersion": "1",
              "workers": [
                {
                  "name": "AutoContext.Worker.DotNet",
                  "endpoint": "autocontext.worker-dotnet",
                  "tools": [{
                    "name": "analyze_shared_name",
                    "description": "x",
                    "parameters": { "content": { "type": "string", "description": "x", "required": true } },
                    "tasks": [{ "name": "task_a" }]
                  }]
                },
                {
                  "name": "AutoContext.Worker.Web",
                  "endpoint": "autocontext.worker-web",
                  "tools": [{
                    "name": "analyze_shared_name",
                    "description": "x",
                    "parameters": { "content": { "type": "string", "description": "x", "required": true } },
                    "tasks": [{ "name": "task_b" }]
                  }]
                }
              ]
            }
            """;
        var registry = RegistryLoader.Parse(json);

        // Act
        var result = RegistrySchemeValidator.Validate(json, registry);

        // Assert
        Assert.Contains(result.Errors, e => e.Contains("Duplicate tool name 'analyze_shared_name'", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_detect_duplicate_worker_endpoint()
    {
        // Arrange
        const string json = """
            {
              "schemaVersion": "1",
              "workers": [
                {
                  "name": "AutoContext.Worker.DotNet",
                  "endpoint": "autocontext.worker-shared",
                  "tools": [{
                    "name": "analyze_one",
                    "description": "x",
                    "parameters": { "content": { "type": "string", "description": "x", "required": true } },
                    "tasks": [{ "name": "task_a" }]
                  }]
                },
                {
                  "name": "AutoContext.Worker.Web",
                  "endpoint": "autocontext.worker-shared",
                  "tools": [{
                    "name": "analyze_two",
                    "description": "x",
                    "parameters": { "content": { "type": "string", "description": "x", "required": true } },
                    "tasks": [{ "name": "task_b" }]
                  }]
                }
              ]
            }
            """;
        var registry = RegistryLoader.Parse(json);

        // Act
        var result = RegistrySchemeValidator.Validate(json, registry);

        // Assert
        Assert.Contains(result.Errors, e => e.Contains("Duplicate worker endpoint 'autocontext.worker-shared'", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_detect_schema_violation_on_bad_endpoint()
    {
        // Arrange — uppercase endpoint violates pattern ^[a-z0-9][a-z0-9.-]*$
        const string json = """
            {
              "schemaVersion": "1",
              "workers": [{
                "name": "AutoContext.Worker.DotNet",
                "endpoint": "AutoContext.WorkerDotnet",
                "tools": [{
                  "name": "analyze_nuget_references",
                  "description": "x",
                  "parameters": { "content": { "type": "string", "description": "x", "required": true } },
                  "tasks": [{ "name": "analyze_nuget_hygiene" }]
                }]
              }]
            }
            """;
        var registry = RegistryLoader.Parse(json);

        // Act
        var result = RegistrySchemeValidator.Validate(json, registry);

        // Assert
        Assert.Multiple(
            () => Assert.False(result.IsValid),
            () => Assert.Contains(result.Errors, e => e.StartsWith("Schema error", StringComparison.Ordinal)));
    }

    [Fact]
    public void Should_throw_ArgumentNullException_for_null_inputs()
    {
        var registry = RegistryLoader.Parse(RegistryEmbeddedResourceLoader.Json);

        Assert.Multiple(
            () => Assert.Throws<ArgumentNullException>(() => RegistrySchemeValidator.Validate(null!, registry)),
            () => Assert.Throws<ArgumentNullException>(() => RegistrySchemeValidator.Validate(RegistryEmbeddedResourceLoader.Json, null!)));
    }
}
