namespace AutoContext.Engine.Protocol.Messages.Logs;

/// <summary>
/// Lowercase wire-string constants for every <see cref="LogRecord.Level"/>
/// value the engine emits on the <c>logs</c> pipe, <c>Logs.Tail*</c>
/// RPC streams, and the <c>Engine.WriteLog</c> notification.
/// Centralised here so engine, workers, and clients reference the
/// same literals without copy-paste drift. Source:
/// <c>design § Log categories</c> and the <c>Engine.WriteLog</c>
/// record shape under <c>design § RPC surface</c>.
/// </summary>
/// <remarks>
/// The literals mirror the lowercased
/// <see cref="Microsoft.Extensions.Logging.LogLevel"/> names without
/// taking a dependency on the logging abstractions package — the
/// protocol assembly stays free of runtime dependencies (P3:
/// wire shape ≠ engine-internal shape). Producers translate from
/// <c>LogLevel</c> to these strings at the seam where the record
/// is shaped for the wire.
/// </remarks>
public static class LogLevels
{
    /// <summary>Most verbose tier; high-frequency tracing.</summary>
    public const string Trace = "trace";

    /// <summary>Diagnostic detail useful during development.</summary>
    public const string Debug = "debug";

    /// <summary>Routine operational records.</summary>
    public const string Information = "information";

    /// <summary>Abnormal-but-recoverable conditions.</summary>
    public const string Warning = "warning";

    /// <summary>Failed operations that affect the current request.</summary>
    public const string Error = "error";

    /// <summary>Process-wide failures.</summary>
    public const string Critical = "critical";
}
