namespace AutoContext.Engine.Core.Tests.Support.Lifecycle;

using System.IO.Pipes;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Protocol;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// Shared helpers for tests that exercise
/// <see cref="PerWorkspaceInstanceGuard"/>. Mirrors the
/// <see cref="LifecycleServiceFixture"/> shape: pure static
/// factories for the guard, its collaborators, and the
/// well-known <c>rpc</c> endpoint name a peer must bind to in
/// order to collide with a guard built from given options.
/// </summary>
public static class PerWorkspaceInstanceGuardFixture
{
    public static EngineOptions CreateOptions() =>
        LifecycleServiceFixture.CreateOptions();

    internal static PipeTransport CreateTransport() =>
        new(NullLogger<PipeTransport>.Instance);

    internal static PerWorkspaceInstanceGuard CreateGuard(EngineOptions options) =>
        new(
            Options.Create(options),
            CreateTransport(),
            NullLogger<PerWorkspaceInstanceGuard>.Instance);

    internal static string ComputeRpcPipeName(EngineOptions options)
    {
        var hash = WorkspaceHash.Compute(options.WorkspacePath);
        return new Endpoint(EndpointKind.Rpc, hash.Value, options.InstanceId).ToString();
    }

    internal static NamedPipeServerStream CreatePeerListener(EngineOptions options) =>
        new(
            ComputeRpcPipeName(options),
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
}
