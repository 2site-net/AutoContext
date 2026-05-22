namespace AutoContext.Engine.Core.Tests.Support.Shared;

internal sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    private readonly DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow()
        => _now;
}
