namespace AutoContext.Build.Tasks.Tests;

public sealed class NoOpTaskTests
{
    [Fact]
    public void Execute_returns_true()
    {
        NoOpTask task = new();

        bool result = task.Execute();

        Assert.True(result);
    }
}
