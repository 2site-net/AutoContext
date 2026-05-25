namespace AutoContext.Engine.Core.Tests.Logging;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Tests.Support.Shared;

using Microsoft.Extensions.Options;

public sealed class RetentionPolicyTests
{
    private static readonly DateTimeOffset KnownNow =
        new(2026, 5, 11, 14, 30, 52, TimeSpan.Zero);

    [Fact]
    public void Should_throw_when_constructed_with_null_options() =>
        Assert.Throws<ArgumentNullException>(() =>
            new RetentionPolicy(options: null!, timeProvider: TimeProvider.System));

    [Fact]
    public void Should_throw_when_constructed_with_null_time_provider() =>
        Assert.Throws<ArgumentNullException>(() =>
            new RetentionPolicy(Options.Create(new EngineOptions()), timeProvider: null!));

    [Fact]
    public void Window_should_reflect_configured_retention()
    {
        // Arrange
        var options = new EngineOptions { Retention = TimeSpan.FromHours(3) };
        var policy = new RetentionPolicy(
            Options.Create(options),
            new FakeTimeProvider(KnownNow));

        // Act / Assert
        Assert.Equal(TimeSpan.FromHours(3), policy.Window);
    }

    [Fact]
    public void IsExpired_should_return_true_for_artefact_older_than_window()
    {
        // Arrange
        var options = new EngineOptions { Retention = TimeSpan.FromMinutes(10) };
        var policy = new RetentionPolicy(
            Options.Create(options),
            new FakeTimeProvider(KnownNow));

        // Act
        var expired = policy.IsExpired(KnownNow - TimeSpan.FromMinutes(15));

        // Assert
        Assert.True(expired);
    }

    [Fact]
    public void IsExpired_should_return_false_for_artefact_within_window()
    {
        // Arrange
        var options = new EngineOptions { Retention = TimeSpan.FromMinutes(10) };
        var policy = new RetentionPolicy(
            Options.Create(options),
            new FakeTimeProvider(KnownNow));

        // Act
        var expired = policy.IsExpired(KnownNow - TimeSpan.FromMinutes(5));

        // Assert
        Assert.False(expired);
    }

    [Fact]
    public void IsExpired_should_return_false_for_future_dated_artefact()
    {
        // Arrange — clock skew across hosts that share a cache
        // root could produce timestamps "ahead" of this engine's
        // clock; those must never be reaped.
        var options = new EngineOptions { Retention = TimeSpan.FromMinutes(10) };
        var policy = new RetentionPolicy(
            Options.Create(options),
            new FakeTimeProvider(KnownNow));

        // Act
        var expired = policy.IsExpired(KnownNow + TimeSpan.FromHours(1));

        // Assert
        Assert.False(expired);
    }

    [Fact]
    public void IsExpired_should_always_return_true_when_window_is_zero()
    {
        // Arrange — TimeSpan.Zero is the "expire immediately"
        // sentinel; even a brand-new artefact is reaped on the
        // next sweep.
        var options = new EngineOptions { Retention = TimeSpan.Zero };
        var policy = new RetentionPolicy(
            Options.Create(options),
            new FakeTimeProvider(KnownNow));

        // Act
        var expired = policy.IsExpired(KnownNow);

        // Assert
        Assert.True(expired);
    }
}
