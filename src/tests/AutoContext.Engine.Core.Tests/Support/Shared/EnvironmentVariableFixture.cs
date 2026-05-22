namespace AutoContext.Engine.Core.Tests.Support.Shared;

internal sealed class EnvironmentVariableFixture : IDisposable
{
    private readonly string _name;
    private readonly string? _original;

    public EnvironmentVariableFixture(string name, string? value)
    {
        _name = name;
        _original = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose()
        => Environment.SetEnvironmentVariable(_name, _original);
}
