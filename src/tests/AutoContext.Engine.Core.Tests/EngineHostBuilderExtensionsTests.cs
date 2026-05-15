namespace AutoContext.Engine.Core.Tests;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Tests.Testing.Utils;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public sealed class EngineHostBuilderExtensionsTests
{
    [Fact]
    public void Should_throw_on_null_builder()
    {
        // Act + Assert
        Assert.Throws<ArgumentNullException>(() =>
            EngineHostBuilderExtensions.AddAutoContextEngine(null!, _ => { }));
    }

    [Fact]
    public void Should_throw_on_null_configure()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() =>
            builder.AddAutoContextEngine(null!));
    }

    [Fact]
    public void Should_return_the_same_builder_for_fluent_chaining()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act
        var result = builder.AddAutoContextEngine(ConfigureValid);

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void Should_run_configure_callback_when_options_are_materialised()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        var callbackRan = false;
        builder.AddAutoContextEngine(options =>
        {
            callbackRan = true;
            ConfigureValid(options);
        });

        // Act
        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<EngineOptions>>().Value;

        // Assert
        Assert.Multiple(
            () => Assert.True(callbackRan),
            () => Assert.Equal(EngineOptionsFakeData.GetWorkspacePath(), options.WorkspacePath),
            () => Assert.Equal(EngineOptionsFakeData.GetInstanceId(), options.InstanceId));
    }

    [Fact]
    public void Should_surface_validation_failures_when_options_are_materialised()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.AddAutoContextEngine(options =>
        {
            options.IdleTimeout = TimeSpan.FromSeconds(-1);
        });
        using var host = builder.Build();
        var resolver = host.Services.GetRequiredService<IOptions<EngineOptions>>();

        // Act
        var ex = Assert.Throws<OptionsValidationException>(() => _ = resolver.Value);

        // Assert
        Assert.Multiple(
            () => Assert.Contains(ex.Failures, m => m.Contains("WorkspacePath", StringComparison.Ordinal)),
            () => Assert.Contains(ex.Failures, m => m.Contains("InstanceId", StringComparison.Ordinal)),
            () => Assert.Contains(ex.Failures, m => m.Contains("IdleTimeout", StringComparison.Ordinal)));
    }

    [Fact]
    public void Should_register_validator_only_once_for_repeat_calls()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.AddAutoContextEngine(ConfigureValid);
        builder.AddAutoContextEngine(ConfigureValid);

        // Act
        using var host = builder.Build();
        var validators = host.Services.GetServices<IValidateOptions<EngineOptions>>();

        // Assert
        Assert.Single(validators, v => v is EngineOptionsValidator);
    }

    private static void ConfigureValid(EngineOptions options)
    {
        options.WorkspacePath = EngineOptionsFakeData.GetWorkspacePath();
        options.InstanceId = EngineOptionsFakeData.GetInstanceId();
    }
}
