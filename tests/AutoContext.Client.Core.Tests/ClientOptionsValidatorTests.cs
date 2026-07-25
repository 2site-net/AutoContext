namespace AutoContext.Client.Core.Tests;

public sealed class ClientOptionsValidatorTests
{
    [Fact]
    public void Valid_options_succeed()
    {
        var result = Validate(ValidOptions());

        Assert.False(result.Failed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_workspace_path_fails(string? workspacePath)
    {
        var options = ValidOptions();
        options.WorkspacePath = workspacePath!;

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void Relative_workspace_path_fails()
    {
        var options = ValidOptions();
        options.WorkspacePath = "relative/workspace";

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void Empty_instance_id_fails()
    {
        var options = ValidOptions();
        options.InstanceId = Guid.Empty;

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void Over_length_instance_label_fails()
    {
        var options = ValidOptions();
        options.InstanceLabel = new string('a', ClientOptions.InstanceLabelMaxLength + 1);

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void Control_character_instance_label_fails()
    {
        var options = ValidOptions();
        options.InstanceLabel = "line\nbreak";

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void Relative_engine_binary_path_fails()
    {
        var options = ValidOptions();
        options.EngineBinaryPath = "engine/autocontext-engine";

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void Negative_idle_timeout_fails()
    {
        var options = ValidOptions();
        options.IdleTimeout = TimeSpan.FromSeconds(-1);

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void Zero_idle_timeout_succeeds()
    {
        var options = ValidOptions();
        options.IdleTimeout = TimeSpan.Zero;

        Assert.False(Validate(options).Failed);
    }

    [Fact]
    public void Null_options_throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ClientOptionsValidator().Validate(name: null, options: null!));
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(ClientOptions options)
        => new ClientOptionsValidator().Validate(name: null, options);

    private static ClientOptions ValidOptions() => new()
    {
        WorkspacePath = OperatingSystem.IsWindows() ? @"C:\workspace" : "/workspace",
        InstanceId = Guid.NewGuid(),
    };
}
