namespace AutoContext.Client.Core.Tests;

using AutoContext.Client.Core.Engine.Rpc;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public sealed class ClientHostBuilderExtensionsTests
{
    [Fact]
    public void Null_builder_throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => ClientHostBuilderExtensions.AddAutoContextClient(builder: null!, _ => { }));
    }

    [Fact]
    public void Null_configure_throws()
    {
        var builder = Host.CreateApplicationBuilder();

        Assert.Throws<ArgumentNullException>(
            () => builder.AddAutoContextClient(configure: null!));
    }

    [Fact]
    public void Returns_the_same_builder_for_chaining()
    {
        var builder = Host.CreateApplicationBuilder();

        var result = builder.AddAutoContextClient(ConfigureValid);

        Assert.Same(builder, result);
    }

    [Fact]
    public void Registers_the_engine_connector()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddAutoContextClient(ConfigureValid);
        using var host = builder.Build();

        Assert.NotNull(host.Services.GetService<EngineConnector>());
    }

    [Fact]
    public async Task Invalid_options_fail_on_start()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddAutoContextClient(options => options.WorkspacePath = string.Empty);
        using var host = builder.Build();

        await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));
    }

    private static void ConfigureValid(ClientOptions options)
    {
        options.WorkspacePath = OperatingSystem.IsWindows() ? @"C:\workspace" : "/workspace";
        options.InstanceId = Guid.NewGuid();
        options.SpawnDisabled = true;
    }
}
