namespace AutoContext.Engine.Tests.Support.Mcp;

using System.Text.Json.Nodes;

using AutoContext.Engine.Tests.Support.Diagnostics;
using AutoContext.Engine.Tests.Support.IO;

/// <summary>
/// Builds a <c>--resources-root</c> overlay tree that routes a fixed set of
/// MCP tools to the standalone <c>AutoContext.Worker.Test.Driver</c> worker.
/// The overlay supplies only <c>workers.json</c>,
/// <c>mcp-tools-registry.json</c>, and <c>mcp-tools-catalog.json</c>; every
/// other side-car (the instructions catalog / manifest, the registry schema)
/// falls through to the bundled <c>Resources</c> tree by the per-file overlay
/// rule, so an engine pointed at this overlay still serves the intrinsic
/// instruction tools alongside the worker-backed test tools.
/// </summary>
public static class TestDriverResourcesOverlay
{
    /// <summary>Worker id the three tools dispatch to.</summary>
    public const string WorkerId = "test-driver";

    /// <summary>Deterministic happy-path tool that echoes its arguments.</summary>
    public const string EchoTool = "test_echo";

    /// <summary>Tool that always reports failure (tool-error arm).</summary>
    public const string FailTool = "test_fail";

    /// <summary>Tool that blocks until cancelled (wait-deadline arm).</summary>
    public const string HangTool = "test_hang";

    /// <summary>
    /// Creates a fresh temporary overlay directory and writes the three
    /// side-cars into it. The caller owns the returned directory and must
    /// dispose it (after any engine reading it has exited).
    /// </summary>
    /// <returns>The overlay directory.</returns>
    /// <exception cref="FileNotFoundException">The test-driver worker binary
    /// has not been built.</exception>
    public static TempDirectory Create()
    {
        var driverCommand = TestDriverWorkerBinaryPath.Value;
        if (!File.Exists(driverCommand))
        {
            throw new FileNotFoundException(
                "Test-driver worker binary not found. Run '.\\build.ps1 DotNet' before running engine integration tests.",
                driverCommand);
        }

        var root = TempDirectory.CreateNew("autocontext-engine-tests-resources");

        var workers = new JsonObject
        {
            ["workers"] = new JsonArray(
                new JsonObject
                {
                    ["id"] = WorkerId,
                    ["type"] = "executable",
                    ["command"] = driverCommand,
                }),
        };

        var registry = new JsonObject
        {
            ["schemaVersion"] = "1",
            ["tools"] = new JsonArray(
                RegistryTool(EchoTool, "Echoes its arguments back verbatim for dispatch round-trip testing."),
                RegistryTool(FailTool, "Always reports failure, exercising the tool-error arm."),
                RegistryTool(HangTool, "Blocks until cancelled, exercising the wait-deadline arm.")),
        };

        var catalog = new JsonObject
        {
            ["schemaVersion"] = "1",
            ["categories"] = new JsonArray(
                new JsonObject
                {
                    ["name"] = "Test Driver",
                    ["description"] = "Deterministic stand-in worker for engine dispatch integration tests.",
                    ["workerId"] = WorkerId,
                }),
            ["tools"] = new JsonArray(
                CatalogTool(EchoTool, "Echoes arguments back."),
                CatalogTool(FailTool, "Always fails."),
                CatalogTool(HangTool, "Blocks until cancelled.")),
        };

        File.WriteAllText(Path.Combine(root.Path, "workers.json"), workers.ToJsonString());
        File.WriteAllText(Path.Combine(root.Path, "mcp-tools-registry.json"), registry.ToJsonString());
        File.WriteAllText(Path.Combine(root.Path, "mcp-tools-catalog.json"), catalog.ToJsonString());

        return root;
    }

    private static JsonObject RegistryTool(string name, string description)
    {
        return new JsonObject
        {
            ["name"] = name,
            ["workerId"] = WorkerId,
            ["description"] = description,
            ["parameters"] = new JsonObject
            {
                ["payload"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Arbitrary string payload echoed back by the test driver.",
                },
            },
        };
    }

    private static JsonObject CatalogTool(string name, string description)
    {
        return new JsonObject
        {
            ["name"] = name,
            ["description"] = description,
            ["category"] = "Test Driver",
        };
    }
}
