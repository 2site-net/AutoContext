namespace AutoContext.Client.Core.Tests;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public sealed class ClientHostBuilderExtensionsTests
{
    [Fact]
    public void Should_throw_when_the_builder_is_null()
        => Assert.Throws<ArgumentNullException>(
            () => ClientHostBuilderExtensions.AddAutoContextClient(builder: null!, _ => { }));

    [Fact]
    public void Should_throw_when_the_configure_callback_is_null()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() => builder.AddAutoContextClient(configure: null!));
    }

    [Fact]
    public void Should_return_the_same_builder_for_chaining()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act
        var result = builder.AddAutoContextClient(ClientOptionsFakeData.ConfigureValid);

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void Should_register_the_engine_connector()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.AddAutoContextClient(ClientOptionsFakeData.ConfigureValid);

        // Act
        using var host = builder.Build();

        // Assert
        Assert.NotNull(host.Services.GetService<EngineConnector>());
    }

    [Fact]
    public async Task Should_fail_on_start_when_the_options_are_invalid()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.AddAutoContextClient(options => options.WorkspacePath = string.Empty);
        using var host = builder.Build();

        // Act + Assert
        await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));
    }
}
