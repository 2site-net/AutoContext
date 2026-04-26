namespace AutoContext.Mcp.Server.Tests.Registry;

using AutoContext.Mcp.Server.Registry;

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
                "id": "dotnet",
                "name": "AutoContext.Worker.DotNet",
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
                  "id": "dotnet",
                  "name": "AutoContext.Worker.DotNet",
                  "tools": [{
                    "name": "analyze_shared_name",
                    "description": "x",
                    "parameters": { "content": { "type": "string", "description": "x", "required": true } },
                    "tasks": [{ "name": "task_a" }]
                  }]
                },
                {
                  "id": "web",
                  "name": "AutoContext.Worker.Web",
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
    public void Should_detect_duplicate_worker_id()
    {
        // Arrange
        const string json = """
            {
              "schemaVersion": "1",
              "workers": [
                {
                  "id": "shared",
                  "name": "AutoContext.Worker.DotNet",
                  "tools": [{
                    "name": "analyze_one",
                    "description": "x",
                    "parameters": { "content": { "type": "string", "description": "x", "required": true } },
                    "tasks": [{ "name": "task_a" }]
                  }]
                },
                {
                  "id": "shared",
                  "name": "AutoContext.Worker.Web",
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
        Assert.Contains(result.Errors, e => e.Contains("Duplicate worker id 'shared'", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_detect_schema_violation_on_bad_name()
    {
        // Arrange — worker name missing the required 'AutoContext.Worker.' prefix
        const string json = """
            {
              "schemaVersion": "1",
              "workers": [{
                "id": "dotnet",
                "name": "SomeOtherWorker",
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
    public void Should_detect_empty_workers_array()
    {
        // Arrange — schema would also reject this, but we want a runtime guard
        // in case the schema is bypassed or its minItems constraint regresses.
        const string json = """
            {
              "schemaVersion": "1",
              "workers": []
            }
            """;
        var registry = new McpWorkersCatalog
        {
            SchemaVersion = "1",
            Workers = [],
        };

        // Act
        var result = RegistrySchemeValidator.Validate(json, registry);

        // Assert
        Assert.Contains(result.Errors, e => e.Contains("Registry contains no workers", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_detect_worker_with_no_tools()
    {
        // Arrange — bypass the schema by constructing the catalog directly
        // to verify the cross-reference check fires independently.
        var registry = new McpWorkersCatalog
        {
            SchemaVersion = "1",
            Workers =
            [
                new McpWorker
                {
                    Id = "dotnet",
                    Name = "AutoContext.Worker.DotNet",
                    Tools = [],
                },
            ],
        };

        // Act — pass valid JSON so we isolate the cross-reference check.
        var result = RegistrySchemeValidator.Validate(RegistryEmbeddedResourceLoader.Json, registry);

        // Assert
        Assert.Contains(result.Errors, e => e.Contains("declares no tools", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_detect_tool_with_no_tasks()
    {
        // Arrange
        var registry = new McpWorkersCatalog
        {
            SchemaVersion = "1",
            Workers =
            [
                new McpWorker
                {
                    Id = "dotnet",
                    Name = "AutoContext.Worker.DotNet",
                    Tools =
                    [
                        new McpToolDefinition
                        {
                            Name = "analyze_something",
                            Description = "x",
                            Parameters = new Dictionary<string, McpToolParameter>
                            {
                                ["content"] = new McpToolParameter { Type = "string", Description = "x", Required = true },
                            },
                            Tasks = [],
                        },
                    ],
                },
            ],
        };

        // Act
        var result = RegistrySchemeValidator.Validate(RegistryEmbeddedResourceLoader.Json, registry);

        // Assert
        Assert.Contains(result.Errors, e => e.Contains("declares no tasks", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_detect_non_numeric_schema_version()
    {
        // Arrange — bypass the schema by constructing the catalog directly so
        // the cross-reference check is exercised in isolation. The runtime
        // guard asserts the *shape* of schemaVersion (must be a non-negative
        // integer); equality with a specific value is enforced by the schema
        // itself (`const: "1"`).
        var registry = new McpWorkersCatalog
        {
            SchemaVersion = "not-a-version",
            Workers =
            [
                new McpWorker
                {
                    Id = "dotnet",
                    Name = "AutoContext.Worker.DotNet",
                    Tools =
                    [
                        new McpToolDefinition
                        {
                            Name = "analyze_something",
                            Description = "x",
                            Parameters = new Dictionary<string, McpToolParameter>
                            {
                                ["content"] = new McpToolParameter { Type = "string", Description = "x", Required = true },
                            },
                            Tasks = [new McpTaskDefinition { Name = "task_a" }],
                        },
                    ],
                },
            ],
        };

        // Act
        var result = RegistrySchemeValidator.Validate(RegistryEmbeddedResourceLoader.Json, registry);

        // Assert
        Assert.Contains(result.Errors, e => e.Contains("schemaVersion 'not-a-version'", StringComparison.Ordinal));
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
