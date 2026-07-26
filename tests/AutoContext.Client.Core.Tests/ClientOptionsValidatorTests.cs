namespace AutoContext.Client.Core.Tests;

using AutoContext.Client.Core.Tests.Support;

public sealed class ClientOptionsValidatorTests
{
    [Fact]
    public void Should_succeed_for_valid_options()
    {
        // Arrange
        var options = ClientOptionsFakeData.CreateValid();

        // Act
        var result = new ClientOptionsValidator().Validate(name: null, options);

        // Assert
        Assert.False(result.Failed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_fail_when_the_workspace_path_is_missing(string? workspacePath)
    {
        // Arrange
        var options = ClientOptionsFakeData.CreateValid();
        options.WorkspacePath = workspacePath!;

        // Act
        var result = new ClientOptionsValidator().Validate(name: null, options);

        // Assert
        Assert.True(result.Failed);
    }

    [Fact]
    public void Should_fail_when_the_workspace_path_is_relative()
    {
        // Arrange
        var options = ClientOptionsFakeData.CreateValid();
        options.WorkspacePath = "relative/workspace";

        // Act
        var result = new ClientOptionsValidator().Validate(name: null, options);

        // Assert
        Assert.True(result.Failed);
    }

    [Fact]
    public void Should_fail_when_the_instance_id_is_empty()
    {
        // Arrange
        var options = ClientOptionsFakeData.CreateValid();
        options.InstanceId = Guid.Empty;

        // Act
        var result = new ClientOptionsValidator().Validate(name: null, options);

        // Assert
        Assert.True(result.Failed);
    }

    [Fact]
    public void Should_fail_when_the_instance_label_exceeds_the_maximum_length()
    {
        // Arrange
        var options = ClientOptionsFakeData.CreateValid();
        options.InstanceLabel = new string('a', ClientOptions.InstanceLabelMaxLength + 1);

        // Act
        var result = new ClientOptionsValidator().Validate(name: null, options);

        // Assert
        Assert.True(result.Failed);
    }

    [Fact]
    public void Should_fail_when_the_instance_label_carries_a_control_character()
    {
        // Arrange
        var options = ClientOptionsFakeData.CreateValid();
        options.InstanceLabel = "line\nbreak";

        // Act
        var result = new ClientOptionsValidator().Validate(name: null, options);

        // Assert
        Assert.True(result.Failed);
    }

    [Fact]
    public void Should_fail_when_the_engine_binary_path_is_relative()
    {
        // Arrange
        var options = ClientOptionsFakeData.CreateValid();
        options.EngineBinaryPath = "engine/autocontext-engine";

        // Act
        var result = new ClientOptionsValidator().Validate(name: null, options);

        // Assert
        Assert.True(result.Failed);
    }

    [Fact]
    public void Should_fail_when_the_idle_timeout_is_negative()
    {
        // Arrange
        var options = ClientOptionsFakeData.CreateValid();
        options.IdleTimeout = TimeSpan.FromSeconds(-1);

        // Act
        var result = new ClientOptionsValidator().Validate(name: null, options);

        // Assert
        Assert.True(result.Failed);
    }

    [Fact]
    public void Should_succeed_when_the_idle_timeout_is_zero()
    {
        // Arrange
        var options = ClientOptionsFakeData.CreateValid();
        options.IdleTimeout = TimeSpan.Zero;

        // Act
        var result = new ClientOptionsValidator().Validate(name: null, options);

        // Assert
        Assert.False(result.Failed);
    }

    [Fact]
    public void Should_throw_when_the_options_are_null()
        => Assert.Throws<ArgumentNullException>(
            () => new ClientOptionsValidator().Validate(name: null, options: null!));
}
