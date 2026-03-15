namespace QaMcp.Tests;

/// <summary>
/// Serialises test classes that read or write <c>tools-status.json</c> via
/// <see cref="ToolsStatusConfig"/> so they do not conflict on the shared file.
/// </summary>
[CollectionDefinition("ToolsStatus")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Required by xUnit collection definition convention.")]
public sealed class ToolsStatusCollection;
