namespace AutoContext.Engine;

using System.CommandLine;
using System.CommandLine.Parsing;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Logging;

/// <summary>
/// <see cref="RootCommand"/> describing the <c>autocontext-engine</c>
/// binary's CLI surface — ten switches in two disjoint roles (daemon
/// vs <c>--mcp-server with-stdio</c>) as defined in
/// <c>design § Engine options (CLI surface)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Each option owns its own argv-level shape check (lowercase
/// UUID, non-negative idle timeout, retention grammar, closed
/// value sets). Cross-option rules — role-conditional rejection
/// of daemon-only switches under the MCP-server role, and the
/// daemon role's <c>--instance-id</c> requirement — live in
/// <see cref="TryBuildOptions"/>, which the binary calls after a
/// successful parse to materialise an <see cref="EngineOptions"/>.
/// Deeper shape (workspace-path absolute-ness, instance-label
/// charset, …) belongs to <c>EngineOptionsValidator</c> in
/// <c>AutoContext.Engine.Core</c>.
/// </para>
/// <para>
/// Every diagnostic the binary writes is prefixed with
/// <c>autocontext-engine: </c> and goes to <b>stderr</b>; stdout
/// is reserved for <c>--version</c> output and, in the
/// <c>--mcp-server with-stdio</c> role, the MCP JSON-RPC channel.
/// </para>
/// </remarks>
internal sealed class EngineCommand : RootCommand
{
    public const string McpServerWithStdioValue = "with-stdio";

    public const string LogRotationSmallValue = "small";

    public const string LogRotationLargeValue = "large";

    public EngineCommand()
        : base("AutoContext Engine binary — pinned, per-workspace host for AutoContext clients.")
    {
        Workspace = new Option<string>("--workspace")
        {
            Description = "Absolute path to the workspace this engine pins to.",
            Required = true,
        };

        InstanceId = new Option<string?>("--instance-id")
        {
            Description = "Lowercase, hyphenated UUIDv4 identifying this engine instance (daemon role only).",
        };
        InstanceId.Validators.Add(ValidateLowercaseGuid);

        InstanceLabel = new Option<string?>("--instance-label")
        {
            Description = "Freeform, observability-only label for this engine instance (daemon role only).",
        };

        IdleTimeoutSeconds = new Option<int?>("--idle-timeout")
        {
            Description = "Idle gate, in seconds; '0' disables the gate (daemon role only).",
        };
        IdleTimeoutSeconds.Validators.Add(ValidateNonNegative);

        ParentProcessId = new Option<int?>("--parent-pid")
        {
            Description = "Positive OS pid; the engine self-exits when that process vanishes (daemon role only).",
        };
        ParentProcessId.Validators.Add(ValidatePositive);

        Retention = new Option<string?>("--retention")
        {
            Description = "Retention window: '0' or '<n>{s|m|h|d}' (daemon role only).",
        };
        Retention.Validators.Add(ValidateRetentionShape);

        LogRotation = new Option<string?>("--log-rotation")
        {
            Description = "Log file rotation size: small (1k lines/5MB) or "
                + "large (5k lines/25MB). Does not change log level. Daemon role only.",
        };
        LogRotation.AcceptOnlyFromAmong(LogRotationSmallValue, LogRotationLargeValue);

        McpServer = new Option<string?>("--mcp-server")
        {
            Description = "Selects the MCP-server role. Only 'with-stdio' is accepted today.",
        };
        McpServer.AcceptOnlyFromAmong(McpServerWithStdioValue);

        CacheRoot = new Option<string?>("--cache-root")
        {
            Description = "Absolute path that overrides the engine cache-root location (both roles).",
        };
        CacheRoot.Validators.Add(ValidateAbsolutePath);

        ResourcesRoot = new Option<string?>("--resources-root")
        {
            Description = "Absolute path that overrides the engine resources-root (side-car) location (both roles).",
        };
        ResourcesRoot.Validators.Add(ValidateAbsolutePath);

        Options.Add(Workspace);
        Options.Add(InstanceId);
        Options.Add(InstanceLabel);
        Options.Add(IdleTimeoutSeconds);
        Options.Add(ParentProcessId);
        Options.Add(Retention);
        Options.Add(LogRotation);
        Options.Add(McpServer);
        Options.Add(CacheRoot);
        Options.Add(ResourcesRoot);
    }

    public Option<string> Workspace { get; }

    public Option<string?> InstanceId { get; }

    public Option<string?> InstanceLabel { get; }

    public Option<int?> IdleTimeoutSeconds { get; }

    public Option<int?> ParentProcessId { get; }

    public Option<string?> Retention { get; }

    public Option<string?> LogRotation { get; }

    public Option<string?> McpServer { get; }

    public Option<string?> CacheRoot { get; }

    public Option<string?> ResourcesRoot { get; }

    /// <summary>
    /// Materialises an <see cref="EngineOptions"/> from a
    /// <see cref="ParseResult"/> that has no parser errors. Enforces
    /// the cross-option role rules and returns
    /// <see langword="false"/> with <paramref name="error"/> set
    /// when the role contract is violated.
    /// </summary>
    public bool TryBuildOptions(
        ParseResult parseResult,
        out EngineOptions options,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        var mcpRaw = parseResult.GetValue(McpServer);
        var isMcpRole = mcpRaw is not null;

        if (isMcpRole)
        {
            if (TryFindDaemonOnlySwitch(parseResult, out var rejected))
            {
                options = new EngineOptions();
                error =
                    $"switch '{rejected}' is not accepted in the --mcp-server with-stdio role";
                return false;
            }
        }
        else
        {
            var idRaw = parseResult.GetValue(InstanceId);
            if (idRaw is null)
            {
                options = new EngineOptions();
                error = "missing required switch '--instance-id' for the daemon role";
                return false;
            }
        }

        options = new EngineOptions
        {
            WorkspacePath = parseResult.GetValue(Workspace) ?? string.Empty,
        };

        var cacheRootValue = parseResult.GetValue(CacheRoot);
        if (cacheRootValue is not null)
        {
            options.CacheRootOverride = cacheRootValue;
        }

        var resourcesRootValue = parseResult.GetValue(ResourcesRoot);
        if (resourcesRootValue is not null)
        {
            options.ResourcesRootOverride = resourcesRootValue;
        }

        if (isMcpRole)
        {
            options.McpServerMode = EngineMcpServerMode.WithStdio;
            error = null;
            return true;
        }

        var instanceIdValue = parseResult.GetValue(InstanceId);
        options.InstanceId = Guid.ParseExact(instanceIdValue!, "D");

        var label = parseResult.GetValue(InstanceLabel);
        if (label is not null)
        {
            options.InstanceLabel = label;
        }

        var idle = parseResult.GetValue(IdleTimeoutSeconds);
        if (idle.HasValue)
        {
            options.IdleTimeout = TimeSpan.FromSeconds(idle.Value);
        }

        var pid = parseResult.GetValue(ParentProcessId);
        if (pid.HasValue)
        {
            options.ParentProcessId = pid.Value;
        }

        var retentionRaw = parseResult.GetValue(Retention);
        if (retentionRaw is not null)
        {
            options.Retention = ParseRetention(retentionRaw);
        }

        var logRotationRaw = parseResult.GetValue(LogRotation);
        if (logRotationRaw is not null)
        {
            options.LogRotation = logRotationRaw switch
            {
                LogRotationLargeValue => LogRotationSize.Large,
                _ => LogRotationSize.Small,
            };
        }

        error = null;
        return true;
    }

    private static void ValidateLowercaseGuid(OptionResult result)
    {
        if (result.Tokens.Count == 0)
        {
            return;
        }

        var raw = result.Tokens[0].Value;
        if (!Guid.TryParseExact(raw, "D", out _))
        {
            result.AddError(
                $"switch '--instance-id' expects a lowercase hyphenated UUIDv4; got '{raw}'");
            return;
        }

        foreach (var ch in raw)
        {
            if (ch is >= 'A' and <= 'F')
            {
                result.AddError(
                    $"switch '--instance-id' expects a lowercase hyphenated UUIDv4; got '{raw}'");
                return;
            }
        }
    }

    private static void ValidateNonNegative(OptionResult result)
    {
        var value = result.GetValueOrDefault<int?>();
        if (value.HasValue && value.Value < 0)
        {
            result.AddError(
                $"switch '--idle-timeout' expects a non-negative integer; got '{value.Value}'");
        }
    }

    private static void ValidatePositive(OptionResult result)
    {
        var value = result.GetValueOrDefault<int?>();
        if (value.HasValue && value.Value <= 0)
        {
            result.AddError(
                $"switch '--parent-pid' expects a positive integer; got '{value.Value}'");
        }
    }

    private static void ValidateRetentionShape(OptionResult result)
    {
        if (result.Tokens.Count == 0)
        {
            return;
        }

        var raw = result.Tokens[0].Value;
        if (!TryParseRetentionValue(raw, out _))
        {
            result.AddError(
                $"switch '--retention' expects '0' or '<n>{{s|m|h|d}}'; got '{raw}'");
        }
    }

    private static void ValidateAbsolutePath(OptionResult result)
    {
        if (result.Tokens.Count == 0)
        {
            return;
        }

        var raw = result.Tokens[0].Value;
        if (string.IsNullOrWhiteSpace(raw) || !Path.IsPathFullyQualified(raw))
        {
            result.AddError(
                $"switch '{result.Option.Name}' expects an absolute path; got '{raw}'");
        }
    }

    private static TimeSpan ParseRetention(string raw)
    {
        return TryParseRetentionValue(raw, out var parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"--retention validator should have rejected '{raw}'.");
    }

    private static bool TryParseRetentionValue(string raw, out TimeSpan retention)
    {
        retention = default;
        if (raw.Length == 0)
        {
            return false;
        }

        if (raw == "0")
        {
            retention = TimeSpan.Zero;
            return true;
        }

        var suffix = raw[^1];
        var digits = raw[..^1];
        if (digits.Length == 0 || !int.TryParse(digits, out var n) || n < 1)
        {
            return false;
        }

        switch (suffix)
        {
            case 's':
                retention = TimeSpan.FromSeconds(n);
                return true;
            case 'm':
                retention = TimeSpan.FromMinutes(n);
                return true;
            case 'h':
                retention = TimeSpan.FromHours(n);
                return true;
            case 'd':
                retention = TimeSpan.FromDays(n);
                return true;
            default:
                return false;
        }
    }

    private bool TryFindDaemonOnlySwitch(ParseResult parseResult, out string switchName)
    {
        if (parseResult.GetValue(InstanceId) is not null)
        {
            switchName = "--instance-id";
            return true;
        }

        if (parseResult.GetValue(InstanceLabel) is not null)
        {
            switchName = "--instance-label";
            return true;
        }

        if (parseResult.GetValue(IdleTimeoutSeconds).HasValue)
        {
            switchName = "--idle-timeout";
            return true;
        }

        if (parseResult.GetValue(ParentProcessId).HasValue)
        {
            switchName = "--parent-pid";
            return true;
        }

        if (parseResult.GetValue(Retention) is not null)
        {
            switchName = "--retention";
            return true;
        }

        if (parseResult.GetValue(LogRotation) is not null)
        {
            switchName = "--log-rotation";
            return true;
        }

        switchName = string.Empty;
        return false;
    }
}
