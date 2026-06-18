namespace AutoContext.Engine.Core.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Infrastructure;

using Microsoft.Extensions.Logging;

/// <summary>
/// Detects the <em>override survival across upgrades</em> pitfall: a
/// workspace-local <c>&lt;name&gt;.instructions.md</c> override keeps
/// winning silently after an engine release refreshes the bundled copy of
/// the same file. For each override that shadows a bundled file, this
/// inspector compares last-write timestamps and emits a warning-level
/// event when the override is older than the bundled file, so a UI can
/// surface the staleness as a non-fatal hint.
/// </summary>
/// <remarks>
/// Overrides that have no bundled counterpart (workspace-only instruction
/// files) are skipped, and a per-file read failure is logged at debug and
/// otherwise ignored so one unreadable file never derails the inspection
/// of the rest.
/// </remarks>
internal sealed partial class InstructionsOverridesStalenessInspector
{
    private readonly EngineResourcesDirectory _bundledInstructions;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates an inspector that compares overrides against the bundled
    /// bodies in <paramref name="bundledInstructions"/>.
    /// </summary>
    /// <param name="bundledInstructions">The instructions directory holding
    /// the bundled <c>*.instructions.md</c> files (override copies shadow
    /// the bundled ones). Must not be <see langword="null"/>.</param>
    /// <param name="logger">Diagnostic sink that carries the staleness
    /// warning.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="bundledInstructions"/> or <paramref name="logger"/>
    /// is <see langword="null"/>.</exception>
    public InstructionsOverridesStalenessInspector(
        EngineResourcesDirectory bundledInstructions,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(bundledInstructions);
        ArgumentNullException.ThrowIfNull(logger);

        _bundledInstructions = bundledInstructions;
        _logger = logger;
    }

    /// <summary>
    /// Inspects every override in <paramref name="overrides"/> and warns
    /// for each one that is older than the bundled file it shadows.
    /// </summary>
    /// <param name="overrides">The override inventory to inspect. Must not
    /// be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="overrides"/>
    /// is <see langword="null"/>.</exception>
    public void Inspect(InstructionsOverridesSnapshot overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        foreach (var fileName in overrides.FileNames)
        {
            if (overrides.TryGetPath(fileName, out var overridePath) && overridePath is not null)
            {
                InspectShadowingOverride(fileName, overridePath);
            }
        }
    }

    private void InspectShadowingOverride(string fileName, string overridePath)
    {
        var bundledPath = _bundledInstructions.ResolveFile(fileName);

        if (!File.Exists(bundledPath))
        {
            return;
        }

        try
        {
            var overrideWriteTimeUtc = File.GetLastWriteTimeUtc(overridePath);
            var bundledWriteTimeUtc = File.GetLastWriteTimeUtc(bundledPath);

            if (overrideWriteTimeUtc < bundledWriteTimeUtc)
            {
                LogOutdatedInstructionsOverride(_logger, fileName, overrideWriteTimeUtc, bundledWriteTimeUtc);
            }
        }
        catch (IOException exception)
        {
            LogInspectionFailed(_logger, fileName, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            LogInspectionFailed(_logger, fileName, exception);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Instruction override '{FileName}' (last modified {OverrideWriteTimeUtc:u}) is older than its bundled file (last modified {BundledWriteTimeUtc:u}); the override may be outdated after an engine upgrade.")]
    private static partial void LogOutdatedInstructionsOverride(ILogger logger, string fileName, DateTime overrideWriteTimeUtc, DateTime bundledWriteTimeUtc);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Failed to compare modification times for instruction override '{FileName}'.")]
    private static partial void LogInspectionFailed(ILogger logger, string fileName, Exception exception);
}
