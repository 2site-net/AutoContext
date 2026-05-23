namespace AutoContext.Engine.Core.Tests.Support.Shared;

using Microsoft.Extensions.Options;

/// <summary>
/// Formats a <see cref="ValidateOptionsResult"/>'s failure messages into
/// a single semicolon-delimited string suitable for use as an xUnit
/// assertion message.
/// </summary>
internal static class ValidateOptionsResultTestFormatter
{
    public static string ReportFailures(ValidateOptionsResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Failures is null ? string.Empty : string.Join("; ", result.Failures);
    }
}
