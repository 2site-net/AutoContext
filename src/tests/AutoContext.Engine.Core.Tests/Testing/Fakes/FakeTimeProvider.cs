namespace AutoContext.Engine.Core.Tests.Testing.Fakes;

internal sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    private readonly DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow()
        => _now;
}
