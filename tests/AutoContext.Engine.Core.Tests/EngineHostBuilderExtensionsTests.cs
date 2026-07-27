namespace AutoContext.Engine.Core.Tests;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Endpoints;
using AutoContext.Engine.Core.Infrastructure.Diagnostics;
using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Core.Machine;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Tests.Support;
using AutoContext.Engine.Core.Workers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

public sealed class EngineHostBuilderExtensionsTests
{
    private static readonly string CacheRootOverride =
        Path.Combine(Path.GetTempPath(), "ac-engine-host-tests");

    private static readonly Action<EngineOptions> ConfigureValid =
        EngineOptionsFakeData.ConfigureValidWith(CacheRootOverride);

    public sealed class AddAutoContextEngine
    {
        [Fact]
        public void Should_drive_the_logger_minimum_level_from_the_options()
        {
            // Arrange
            var builder = Host.CreateApplicationBuilder();
            builder.AddAutoContextEngine(options =>
            {
                ConfigureValid(options);
                options.LogLevel = LogLevel.Debug;
            });

            // Act
            using var host = builder.Build();
            var filter = host.Services.GetRequiredService<IOptions<LoggerFilterOptions>>().Value;

            // Assert
            Assert.Equal(LogLevel.Debug, filter.MinLevel);
        }

        [Fact]
        public void Should_leave_the_host_minimum_level_alone_when_no_level_is_set()
        {
            // Arrange
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.SetMinimumLevel(LogLevel.Trace);
            builder.AddAutoContextEngine(ConfigureValid);

            // Act
            using var host = builder.Build();
            var filter = host.Services.GetRequiredService<IOptions<LoggerFilterOptions>>().Value;

            // Assert
            Assert.Equal(LogLevel.Trace, filter.MinLevel);
        }

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

        [Fact]
        public void Should_register_EndpointHostService_as_a_hosted_service()
        {
            // Arrange
            var builder = Host.CreateApplicationBuilder();
            builder.AddAutoContextEngine(ConfigureValid);

            // Act
            using var host = builder.Build();
            var hosted = host.Services.GetServices<IHostedService>();

            // Assert
            Assert.Single(hosted, h => h is EndpointHostService);
        }

        [Fact]
        public void Should_register_EndpointHostService_only_once_for_repeat_calls()
        {
            // Arrange
            var builder = Host.CreateApplicationBuilder();
            builder.AddAutoContextEngine(ConfigureValid);
            builder.AddAutoContextEngine(ConfigureValid);

            // Act
            using var host = builder.Build();
            var hosted = host.Services.GetServices<IHostedService>();

            // Assert
            Assert.Single(hosted, h => h is EndpointHostService);
        }

        [Fact]
        public void Should_register_RegistryFileService_as_singleton_and_hosted_service()
        {
            // Arrange
            var builder = Host.CreateApplicationBuilder();
            builder.AddAutoContextEngine(ConfigureValid);

            // Act
            using var host = builder.Build();
            var registry = host.Services.GetRequiredService<RegistryFileService>();
            var hosted = host.Services.GetServices<IHostedService>().ToList();

            // Assert
            Assert.Multiple(
                () => Assert.Same(registry, host.Services.GetRequiredService<RegistryFileService>()),
                () => Assert.Single(hosted, h => ReferenceEquals(h, registry)),
                () => Assert.Equal(
                    Path.Combine(CacheRootPathResolver.Resolve(CacheRootOverride), EngineCacheLayout.RegistryFileName),
                    registry.Path));
        }

        [Fact]
        public void Should_register_RegistryFileService_only_once_for_repeat_calls()
        {
            // Arrange
            var builder = Host.CreateApplicationBuilder();
            builder.AddAutoContextEngine(ConfigureValid);
            builder.AddAutoContextEngine(ConfigureValid);

            // Act
            using var host = builder.Build();
            var hosted = host.Services.GetServices<IHostedService>().ToList();

            // Assert
            Assert.Single(hosted, h => h is RegistryFileService);
        }

        [Fact]
        public void Should_register_WorkerProcessService_as_a_singleton()
        {
            // Arrange
            var builder = Host.CreateApplicationBuilder();

            // Act
            builder.AddAutoContextEngine(ConfigureValid);

            // Assert
            var descriptor = Assert.Single(
                builder.Services, d => d.ServiceType == typeof(WorkerProcessService));
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void Should_register_the_worker_process_launcher_seam_as_a_singleton()
        {
            // Arrange
            var builder = Host.CreateApplicationBuilder();

            // Act
            builder.AddAutoContextEngine(ConfigureValid);

            // Assert
            var descriptor = Assert.Single(
                builder.Services, d => d.ServiceType == typeof(IProcessLauncher<WorkerProcessInfo>));
            Assert.Multiple(
                () => Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime),
                () => Assert.Equal(typeof(WorkerProcessLauncher), descriptor.ImplementationType));
        }

        [Fact]
        public void Should_register_the_worker_connection_probe_seam_as_a_singleton()
        {
            // Arrange
            var builder = Host.CreateApplicationBuilder();

            // Act
            builder.AddAutoContextEngine(ConfigureValid);

            // Assert
            var descriptor = Assert.Single(
                builder.Services, d => d.ServiceType == typeof(IWorkerConnectionProbe));
            Assert.Multiple(
                () => Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime),
                () => Assert.Equal(typeof(WorkerConnectionProbe), descriptor.ImplementationType));
        }

        [Fact]
        public void Should_order_hosted_services_so_stop_runs_lifecycle_then_file_service()
        {
            // Arrange
            var builder = Host.CreateApplicationBuilder();
            builder.AddAutoContextEngine(ConfigureValid);

            // Act
            using var host = builder.Build();
            var hosted = host.Services.GetServices<IHostedService>().ToList();

            var indexOfFile = hosted.FindIndex(h => h is RegistryFileService);
            var indexOfEndpointHost = hosted.FindIndex(h => h is EndpointHostService);

            // Assert
            Assert.Multiple(
                () => Assert.True(indexOfFile >= 0, "RegistryFileService should be registered."),
                () => Assert.True(indexOfEndpointHost >= 0, "EndpointHostService should be registered."),
                () => Assert.True(indexOfFile < indexOfEndpointHost, "RegistryFileService must register before EndpointHostService so it stops last."));
        }

        [Fact]
        public void Should_register_TimeProvider_as_singleton_defaulting_to_system_clock()
        {
            // Arrange
            var builder = Host.CreateApplicationBuilder();
            builder.AddAutoContextEngine(ConfigureValid);

            // Act
            using var host = builder.Build();
            var clock = host.Services.GetRequiredService<TimeProvider>();

            // Assert
            Assert.Multiple(
                () => Assert.Same(clock, host.Services.GetRequiredService<TimeProvider>()),
                () => Assert.Same(TimeProvider.System, clock));
        }

        [Fact]
        public void Should_not_displace_pre_registered_TimeProvider()
        {
            // Arrange
            var builder = Host.CreateApplicationBuilder();
            var fake = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
            builder.Services.AddSingleton<TimeProvider>(fake);
            builder.AddAutoContextEngine(ConfigureValid);

            // Act
            using var host = builder.Build();
            var clock = host.Services.GetRequiredService<TimeProvider>();

            // Assert
            Assert.Same(fake, clock);
        }
    }
}
