namespace AutoContext.Mcp.Server.Tests.Support.Tools.Invocation;

/// <summary>
/// Thread-safe peak-concurrency tracker used by
/// <c>ToolInvoker</c> concurrency tests to assert how many task
/// invocations were in flight at the same time.
/// </summary>
internal sealed class ConcurrencyObserver
{
    private readonly Lock _gate = new();
    private int _current;

    public int MaxConcurrent { get; private set; }

    public void Enter()
    {
        lock (_gate)
        {
            _current++;
            if (_current > MaxConcurrent)
            {
                MaxConcurrent = _current;
            }
        }
    }

    public void Exit()
    {
        lock (_gate)
        {
            _current--;
        }
    }
}
