namespace AutoContext.Engine.Core;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates an <see cref="EngineOptions"/> instance against the
/// shape rules in
/// <c>design § Engine options (CLI surface)</c>. Surfaces every
/// violation in one pass so callers see all problems at once
/// rather than fixing them one boot at a time.
/// </summary>
/// <remarks>
/// The validator only checks shape: that values fall within the
/// documented ranges and charsets. It does not perform semantic
/// gating such as confirming <see cref="EngineOptions.WorkspacePath"/>
/// resolves to an existing directory or that
/// <see cref="EngineOptions.ParentProcessId"/> corresponds to a
/// live process — those checks belong to the hosted services that
/// consume the options.
/// </remarks>
internal sealed class EngineOptionsValidator : IValidateOptions<EngineOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, EngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string>? failures = null;

        if (string.IsNullOrWhiteSpace(options.WorkspacePath))
        {
            (failures ??= []).Add(
                $"{nameof(EngineOptions.WorkspacePath)} is required.");
        }
        else
        {
            if (!Path.IsPathFullyQualified(options.WorkspacePath))
            {
                (failures ??= []).Add(
                    $"{nameof(EngineOptions.WorkspacePath)} must be an absolute path; got '{options.WorkspacePath}'.");
            }
        }

        if (options.InstanceId == Guid.Empty)
        {
            (failures ??= []).Add(
                $"{nameof(EngineOptions.InstanceId)} is required and must be a non-empty UUID.");
        }

        if (!IsValidInstanceLabel(options.InstanceLabel))
        {
            (failures ??= []).Add(
                $"{nameof(EngineOptions.InstanceLabel)} must be at most "
                + $"{EngineOptions.InstanceLabelMaxLength} printable-ASCII characters "
                + "with no control characters or newlines.");
        }

        if (options.IdleTimeout < TimeSpan.Zero)
        {
            (failures ??= []).Add(
                $"{nameof(EngineOptions.IdleTimeout)} must be non-negative; got {options.IdleTimeout}.");
        }

        if (options.ParentProcessId is { } pid && pid <= 0)
        {
            (failures ??= []).Add(
                $"{nameof(EngineOptions.ParentProcessId)} must be a positive integer when set; got {pid}.");
        }

        if (options.Retention < TimeSpan.Zero)
        {
            (failures ??= []).Add(
                $"{nameof(EngineOptions.Retention)} must be non-negative; got {options.Retention}.");
        }

        if (!Enum.IsDefined(options.Logging))
        {
            (failures ??= []).Add(
                $"{nameof(EngineOptions.Logging)} '{options.Logging}' is not a defined "
                + $"{nameof(EngineLoggingVerbosity)} value.");
        }

        if (!Enum.IsDefined(options.McpServerMode))
        {
            (failures ??= []).Add(
                $"{nameof(EngineOptions.McpServerMode)} '{options.McpServerMode}' is not a defined "
                + $"{nameof(EngineMcpServerMode)} value.");
        }

        if (options.CorpusRootOverride is { } corpusRoot
            && !Path.IsPathFullyQualified(corpusRoot))
        {
            (failures ??= []).Add(
                $"{nameof(EngineOptions.CorpusRootOverride)} must be an absolute path when set; got '{corpusRoot}'.");
        }

        return failures is null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidInstanceLabel(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        if (label.Length == 0)
        {
            return true;
        }

        if (label.Length > EngineOptions.InstanceLabelMaxLength)
        {
            return false;
        }

        foreach (var ch in label)
        {
            // Printable ASCII per design § Engine options >
            // --instance-label: 0x20..0x7E inclusive, no control
            // characters, no newlines, no tabs.
            if (ch is < (char)0x20 or > (char)0x7E)
            {
                return false;
            }
        }

        return true;
    }
}
