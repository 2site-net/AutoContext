namespace AutoContext.Engine.Tests.Support.Environment;

public sealed class EnvironmentVariableFixture : IDisposable
{
    private readonly string _name;
    private readonly string? _original;

    public EnvironmentVariableFixture(string name, string? value)
    {
        _name = name;
        _original = System.Environment.GetEnvironmentVariable(name);
        System.Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose()
        => System.Environment.SetEnvironmentVariable(_name, _original);
}
