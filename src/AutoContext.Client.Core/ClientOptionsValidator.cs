namespace AutoContext.Client.Core;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates a <see cref="ClientOptions"/> instance against its shape
/// rules in one pass, so a caller sees every problem at once rather
/// than fixing them one boot at a time. Checks shape only — path
/// rootedness, non-empty instance id, label charset and length — not
/// semantics such as whether <see cref="ClientOptions.WorkspacePath"/>
/// exists on disk or whether an engine is reachable.
/// </summary>
internal sealed class ClientOptionsValidator : IValidateOptions<ClientOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string>? failures = null;

        if (string.IsNullOrWhiteSpace(options.WorkspacePath))
        {
            (failures ??= []).Add(
                $"{nameof(ClientOptions.WorkspacePath)} is required.");
        }
        else if (!Path.IsPathFullyQualified(options.WorkspacePath))
        {
            (failures ??= []).Add(
                $"{nameof(ClientOptions.WorkspacePath)} must be an absolute path; got '{options.WorkspacePath}'.");
        }

        if (options.InstanceId == Guid.Empty)
        {
            (failures ??= []).Add(
                $"{nameof(ClientOptions.InstanceId)} is required and must be a non-empty UUID.");
        }

        if (!IsValidInstanceLabel(options.InstanceLabel))
        {
            (failures ??= []).Add(
                $"{nameof(ClientOptions.InstanceLabel)} must be at most "
                + $"{ClientOptions.InstanceLabelMaxLength} printable-ASCII characters "
                + "with no control characters or newlines.");
        }

        if (options.EngineBinaryPath is { } binaryPath && !Path.IsPathFullyQualified(binaryPath))
        {
            (failures ??= []).Add(
                $"{nameof(ClientOptions.EngineBinaryPath)} must be an absolute path when set; got '{binaryPath}'.");
        }

        if (options.IdleTimeout is { } idleTimeout && idleTimeout < TimeSpan.Zero)
        {
            (failures ??= []).Add(
                $"{nameof(ClientOptions.IdleTimeout)} must be non-negative when set; got {idleTimeout}.");
        }

        return failures is null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidInstanceLabel(string? label)
    {
        if (label is null)
        {
            return false;
        }

        if (label.Length > ClientOptions.InstanceLabelMaxLength)
        {
            return false;
        }

        foreach (var character in label)
        {
            if (character is < ' ' or > '~')
            {
                return false;
            }
        }

        return true;
    }
}
