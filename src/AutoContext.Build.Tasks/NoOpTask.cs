namespace AutoContext.Build.Tasks;

/// <summary>
/// Placeholder MSBuild task that proves the build-tasks assembly loads and the
/// MSBuild integration compiles. The real instruction-corpus build tasks land
/// in later Phase 5 rows; this type only exists to give the scaffold a
/// compilable, testable surface.
/// </summary>
public sealed class NoOpTask : Microsoft.Build.Utilities.Task
{
    /// <inheritdoc />
    public override bool Execute()
        => true;
}
