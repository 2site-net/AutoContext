namespace AutoContext.Engine.Core.Machine;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

/// <summary>
/// Paranoid last-gasp writer of <c>crash.log</c> under the
/// per-instance subtree
/// <c>&lt;cacheRoot&gt;\&lt;workspaceHash&gt;\&lt;instanceId&gt;\logs\crash.log</c>.
/// Instances are constructed once at daemon startup and captured
/// by the three unhandled-exception sinks
/// (<c>DaemonHostFactory.RunAsync</c> top-level try/catch,
/// <see cref="AppDomain.UnhandledException"/>,
/// <see cref="TaskScheduler.UnobservedTaskException"/>).
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not registered with DI and deliberately holds no
/// <see cref="Microsoft.Extensions.Logging.ILogger"/>: the writer
/// must run from inside catch handlers and finalizer-adjacent
/// contexts where the host's services may already be torn down.
/// The target path is pre-composed in the constructor so the
/// fast path on a real crash is a single
/// <see cref="Directory.CreateDirectory(string)"/> +
/// <see cref="File.AppendAllText(string, string?)"/> pair.
/// </para>
/// <para>
/// Output is newline-delimited JSON: each
/// <see cref="TryWrite(Exception, string)"/> call appends one
/// record terminated by <see cref="Environment.NewLine"/>, so
/// concurrent or sequential fires across the three sinks all
/// survive in order. Readers split on newline and parse each
/// line. See <c>design § Lifecycle &gt; EngineCrashWriter</c>.
/// </para>
/// <para>
/// Every I/O failure inside <see cref="TryWrite(Exception, string)"/>
/// is intentionally swallowed: the writer's contract is "do not
/// mask the original fault". A read-only target directory, a
/// pre-existing file where the <c>logs</c> directory should be,
/// or any other write failure all yield silent best-effort
/// behaviour while the original exception continues to propagate
/// and take the process down with its own non-zero exit code.
/// </para>
/// <para>
/// The writer is never invoked from graceful shutdown paths
/// (<c>Engine.Shutdown</c> RPC, idle-timeout watchdog,
/// parent-pid host watchdog). Those flows complete cleanly and
/// therefore never trip any of the three sinks; no runtime gate
/// is required.
/// </para>
/// <para>
/// The type is public because the binary composition root
/// (<c>AutoContext.Engine.DaemonHostFactory</c>) lives in a
/// sibling assembly that does not have
/// <c>InternalsVisibleTo</c> access to
/// <c>AutoContext.Engine.Core</c>, and embedders that compose
/// the engine in-process may want to install the same sinks
/// around their own host runner.
/// </para>
/// </remarks>
public sealed class EngineCrashWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly EngineCacheLayout _cacheLayout;

    /// <summary>
    /// Creates a new <see cref="EngineCrashWriter"/> targeted at
    /// the per-instance <c>crash.log</c> resolved by
    /// <paramref name="cacheLayout"/>. The target path is captured
    /// eagerly so the catch-handler fast path skips path work.
    /// </summary>
    /// <param name="cacheLayout">Resolved engine cache-root layout
    /// the crash log path is read from.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="cacheLayout"/> is <see langword="null"/>.
    /// </exception>
    public EngineCrashWriter(EngineCacheLayout cacheLayout)
    {
        ArgumentNullException.ThrowIfNull(cacheLayout);

        _cacheLayout = cacheLayout;

        CrashLogFilePath = cacheLayout.CrashLogFilePath;
    }

    /// <summary>
    /// Absolute path of the <c>crash.log</c> file this writer
    /// appends to. Exposed for diagnostics and tests; the file
    /// is not created until the first successful
    /// <see cref="TryWrite(Exception, string)"/> call.
    /// </summary>
    public string CrashLogFilePath { get; }

    /// <summary>
    /// Appends one NDJSON record describing
    /// <paramref name="exception"/> to <see cref="CrashLogFilePath"/>.
    /// Returns silently if <paramref name="exception"/> is
    /// <see langword="null"/>, <paramref name="source"/> is null
    /// or empty, or any I/O step fails. Never throws.
    /// </summary>
    /// <param name="exception">The unhandled exception to
    /// record.</param>
    /// <param name="source">Short label identifying which sink
    /// observed the fault (e.g. <c>"DaemonHostFactory.RunAsync"</c>,
    /// <c>"AppDomain.UnhandledException"</c>,
    /// <c>"TaskScheduler.UnobservedTaskException"</c>).</param>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Crash writer contract: every I/O failure is intentionally swallowed so the original fault still propagates and takes the process down with its own exit code.")]
    public void TryWrite(Exception exception, string source)
    {
        if (exception is null || string.IsNullOrEmpty(source))
        {
            return;
        }

        try
        {
            var record = new CrashRecord(
                Timestamp: DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                Source: source,
                InstanceId: _cacheLayout.CacheRoot.InstanceId,
                WorkspacePath: _cacheLayout.CacheRoot.WorkspaceUserPath,
                Exception: BuildExceptionRecord(exception));

            var json = JsonSerializer.Serialize(record, JsonOptions);

            var directory = Path.GetDirectoryName(CrashLogFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(CrashLogFilePath, json + Environment.NewLine);
        }
        catch
        {
            // Paranoid swallow — see type-level remarks.
        }
    }

    private static CrashExceptionRecord BuildExceptionRecord(Exception exception) =>
        new(
            Type: exception.GetType().FullName ?? exception.GetType().Name,
            Message: exception.Message,
            StackTrace: exception.StackTrace,
            Inner: exception.InnerException is null
                ? null
                : BuildExceptionRecord(exception.InnerException));

    private sealed record CrashRecord(
        string Timestamp,
        string Source,
        string InstanceId,
        string WorkspacePath,
        CrashExceptionRecord Exception);

    private sealed record CrashExceptionRecord(
        string Type,
        string Message,
        string? StackTrace,
        CrashExceptionRecord? Inner);
}
