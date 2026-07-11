namespace AutoContext.Engine.Core.Tests.Workers;

using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Core.Workers;
using AutoContext.Engine.Core.Workers.Format;
using AutoContext.Engine.Protocol;

public sealed class WorkerProcessInfoResolverTests
{
    public sealed class Resolve
    {
        private const string InstanceId = "11111111-1111-1111-1111-111111111111";
        private const string WorkspacePath = "/home/user/project";

        private static readonly string WorkersDirectory =
            Path.Combine(Path.GetTempPath(), "ac-workers");

        private static readonly string EngineLogArgument =
            "log=" + new Endpoint(
                EndpointKind.Rpc,
                WorkspaceHash.Compute(WorkspacePath).Value,
                Guid.Parse(InstanceId)).ToString();

        [Fact]
        public void Should_reject_null_manifest()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => WorkerProcessInfoResolver.Resolve(
                    null!, WorkersDirectory, InstanceId, WorkspacePath));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Should_reject_missing_workers_directory(string? workersDirectory)
        {
            // Arrange
            var manifest = new JsonWorkersManifest(
                [new JsonWorkerEntry(Id: "dotnet", Command: "${root}/AutoContext.Worker.DotNet")]);

            // Act + Assert
            Assert.ThrowsAny<ArgumentException>(
                () => WorkerProcessInfoResolver.Resolve(
                    manifest, workersDirectory!, InstanceId, WorkspacePath));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Should_reject_missing_instance_id(string? instanceId)
        {
            // Arrange
            var manifest = new JsonWorkersManifest(
                [new JsonWorkerEntry(Id: "dotnet", Command: "${root}/AutoContext.Worker.DotNet")]);

            // Act + Assert
            Assert.ThrowsAny<ArgumentException>(
                () => WorkerProcessInfoResolver.Resolve(
                    manifest, WorkersDirectory, instanceId!, WorkspacePath));
        }

        [Fact]
        public void Should_throw_when_workers_array_is_missing()
        {
            // Arrange
            var manifest = new JsonWorkersManifest(Workers: null);

            // Act + Assert
            Assert.Throws<InvalidOperationException>(
                () => WorkerProcessInfoResolver.Resolve(
                    manifest, WorkersDirectory, InstanceId, WorkspacePath));
        }

        [Fact]
        public void Should_throw_when_a_row_is_missing_its_id()
        {
            // Arrange
            var manifest = new JsonWorkersManifest(
                [new JsonWorkerEntry(Command: "${root}/AutoContext.Worker.DotNet")]);

            // Act + Assert
            Assert.Throws<InvalidOperationException>(
                () => WorkerProcessInfoResolver.Resolve(
                    manifest, WorkersDirectory, InstanceId, WorkspacePath));
        }

        [Fact]
        public void Should_throw_when_a_row_is_missing_its_command()
        {
            // Arrange
            var manifest = new JsonWorkersManifest([new JsonWorkerEntry(Id: "dotnet")]);

            // Act + Assert
            Assert.Throws<InvalidOperationException>(
                () => WorkerProcessInfoResolver.Resolve(
                    manifest, WorkersDirectory, InstanceId, WorkspacePath));
        }

        [Fact]
        public void Should_throw_when_a_worker_id_is_duplicated()
        {
            // Arrange
            var manifest = new JsonWorkersManifest(
            [
                new JsonWorkerEntry(Id: "dotnet", Command: "${root}/AutoContext.Worker.Foo"),
                new JsonWorkerEntry(Id: "dotnet", Command: "${root}/AutoContext.Worker.Bar"),
            ]);

            // Act + Assert
            Assert.Throws<InvalidOperationException>(
                () => WorkerProcessInfoResolver.Resolve(
                    manifest, WorkersDirectory, InstanceId, WorkspacePath));
        }

        [Fact]
        public void Should_resolve_a_staged_executable_command()
        {
            // Arrange
            var manifest = new JsonWorkersManifest(
                [new JsonWorkerEntry(Id: "dotnet", Type: "executable", Command: "${root}/AutoContext.Worker.DotNet")]);
            var expectedCommand = Path.Combine(WorkersDirectory, "dotnet", "AutoContext.Worker.DotNet")
                + (OperatingSystem.IsWindows() ? ".exe" : string.Empty);

            // Act
            var info = Assert.Single(
                WorkerProcessInfoResolver.Resolve(manifest, WorkersDirectory, InstanceId, WorkspacePath));
            var expectedArguments = new[] { "--instance-id", InstanceId, "--service", EngineLogArgument };

            // Assert
            Assert.Multiple(
                () => Assert.Equal("dotnet", info.WorkerId),
                () => Assert.Equal(expectedCommand, info.Command),
                () => Assert.Equal(expectedArguments, info.Arguments),
                () => Assert.Equal($"autocontext.worker-dotnet#{InstanceId}", info.Endpoint));
        }

        [Fact]
        public void Should_resolve_a_launcher_command_keeping_the_launcher_as_executable()
        {
            // Arrange
            var manifest = new JsonWorkersManifest(
                [new JsonWorkerEntry(Id: "web", Type: "script", Command: "node ${root}/index.js")]);
            var expectedScript = Path.Combine(WorkersDirectory, "web", "index.js");

            // Act
            var info = Assert.Single(
                WorkerProcessInfoResolver.Resolve(manifest, WorkersDirectory, InstanceId, WorkspacePath));
            var expectedArguments = new[] { expectedScript, "--instance-id", InstanceId, "--service", EngineLogArgument };

            // Assert
            Assert.Multiple(
                () => Assert.Equal("web", info.WorkerId),
                () => Assert.Equal("node", info.Command),
                () => Assert.Equal(expectedArguments, info.Arguments),
                () => Assert.Equal($"autocontext.worker-web#{InstanceId}", info.Endpoint));
        }

        [Fact]
        public void Should_append_workspace_root_to_the_workspace_worker_only()
        {
            // Arrange
            var manifest = new JsonWorkersManifest(
                [new JsonWorkerEntry(Id: "workspace", Type: "executable", Command: "${root}/AutoContext.Worker.Workspace")]);

            // Act
            var info = Assert.Single(
                WorkerProcessInfoResolver.Resolve(manifest, WorkersDirectory, InstanceId, WorkspacePath));
            var expectedArguments = new[] { "--instance-id", InstanceId, "--workspace-root", WorkspacePath, "--service", EngineLogArgument };

            // Assert
            Assert.Equal(expectedArguments, info.Arguments);
        }

        [Fact]
        public void Should_omit_workspace_root_when_the_workspace_path_is_empty()
        {
            // Arrange
            var manifest = new JsonWorkersManifest(
                [new JsonWorkerEntry(Id: "workspace", Type: "executable", Command: "${root}/AutoContext.Worker.Workspace")]);

            // Act
            var info = Assert.Single(
                WorkerProcessInfoResolver.Resolve(manifest, WorkersDirectory, InstanceId, string.Empty));

            // Assert
            Assert.DoesNotContain("--workspace-root", info.Arguments);
        }

        [Fact]
        public void Should_not_append_workspace_root_to_a_non_workspace_worker()
        {
            // Arrange
            var manifest = new JsonWorkersManifest(
                [new JsonWorkerEntry(Id: "dotnet", Type: "executable", Command: "${root}/AutoContext.Worker.DotNet")]);

            // Act
            var info = Assert.Single(
                WorkerProcessInfoResolver.Resolve(manifest, WorkersDirectory, InstanceId, WorkspacePath));

            // Assert
            Assert.DoesNotContain("--workspace-root", info.Arguments);
        }

        [Fact]
        public void Should_resolve_every_row_in_manifest_order()
        {
            // Arrange
            var manifest = new JsonWorkersManifest(
            [
                new JsonWorkerEntry(Id: "dotnet", Type: "executable", Command: "${root}/AutoContext.Worker.DotNet"),
                new JsonWorkerEntry(Id: "web", Type: "script", Command: "node ${root}/index.js"),
                new JsonWorkerEntry(Id: "workspace", Type: "executable", Command: "${root}/AutoContext.Worker.Workspace"),
            ]);

            // Act
            var infos = WorkerProcessInfoResolver.Resolve(manifest, WorkersDirectory, InstanceId, WorkspacePath);
            var expectedOrder = new[] { "dotnet", "web", "workspace" };

            // Assert
            Assert.Equal(
                expectedOrder,
                infos.Select(info => info.WorkerId));
        }
    }
}
