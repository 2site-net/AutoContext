namespace AutoContext.Client.Core.Tests.Support;

using AutoContext.Client.Core;

/// <summary>
/// Valid <see cref="ClientOptions"/> shapes for the options-validation
/// and host-registration tests.
/// </summary>
internal static class ClientOptionsFakeData
{
    public static ClientOptions CreateValid()
        => new()
        {
            WorkspacePath = OperatingSystem.IsWindows() ? @"C:\workspace" : "/workspace",
            InstanceId = Guid.NewGuid(),
            SpawnDisabled = true,
        };

    public static void ConfigureValid(ClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var valid = CreateValid();

        options.WorkspacePath = valid.WorkspacePath;
        options.InstanceId = valid.InstanceId;
        options.SpawnDisabled = valid.SpawnDisabled;
    }
}
