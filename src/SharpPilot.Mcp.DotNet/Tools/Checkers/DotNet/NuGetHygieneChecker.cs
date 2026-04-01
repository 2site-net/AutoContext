namespace SharpPilot.Mcp.DotNet.Tools.Checkers.DotNet;

using System.ComponentModel;
using System.Xml.Linq;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Server;

using SharpPilot.Mcp.DotNet.Tools.Checkers;

/// <summary>
/// Validates NuGet package hygiene in .csproj files: no duplicate references,
/// no floating versions, no missing versions (unless Central Package Management),
/// and flags packages that have well-known built-in .NET alternatives.
/// </summary>
[McpServerToolType]
public sealed partial class NuGetHygieneChecker(ILogger<NuGetHygieneChecker> logger) : IChecker
{
    /// <inheritdoc />
    public string ToolName
        => "check_nuget_hygiene";

    /// <summary>
    /// Maps package names (case-insensitive) to their built-in .NET alternative.
    /// </summary>
    private static readonly Dictionary<string, string> BuiltInAlternatives = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Newtonsoft.Json"] = "System.Text.Json",
        ["AutoMapper"] = "manual mapping or Mapster",
        ["FluentValidation"] = "System.ComponentModel.DataAnnotations or custom validation",
        ["MediatR"] = "built-in DI and direct service calls",
        ["Polly"] = "Microsoft.Extensions.Http.Resilience (for .NET 8+)",
        ["RestSharp"] = "System.Net.Http.HttpClient",
        ["Dapper"] = "Entity Framework Core or ADO.NET",
    };

    /// <summary>
    /// Checks a .csproj file for NuGet hygiene violations.
    /// </summary>
    [McpServerTool(Name = "check_nuget_hygiene", ReadOnly = true, Idempotent = true)]
    [Description(
        "Checks a .csproj file for NuGet package hygiene: " +
        "no duplicate PackageReference entries, " +
        "no floating or wildcard versions (e.g., '*', version ranges), " +
        "no PackageReference without a Version attribute (unless Central Package Management is enabled via ManagePackageVersionsCentrally), " +
        "and flags packages that have well-known built-in .NET alternatives.")]
    public Task<string> CheckAsync(
        [Description("The .csproj file content (XML) to check.")]
        string content,
        IReadOnlyDictionary<string, string>? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        LogToolInvoked(logger, ToolName, content.Length);

        XDocument doc;

        try
        {
            doc = XDocument.Parse(content);
        }
        catch (System.Xml.XmlException ex)
        {
            return Task.FromResult($"❌ Failed to parse .csproj XML: {ex.Message}");
        }

        if (doc.Root is null)
        {
            return Task.FromResult("❌ Failed to parse .csproj XML: document has no root element.");
        }

        var violations = new List<string>();
        var packages = GetPackageReferences(doc.Root);
        var usesCpm = UsesCentralPackageManagement(doc.Root);

        CheckDuplicatePackages(packages, violations);
        CheckFloatingVersions(packages, violations);
        CheckMissingVersions(packages, usesCpm, violations);
        CheckBuiltInAlternatives(packages, violations);

        return Task.FromResult(violations.Count == 0
            ? "✅ NuGet hygiene is correct."
            : $"❌ Found {violations.Count} NuGet hygiene violation(s):\n" +
              string.Join('\n', violations.Select((v, i) => $"  {i + 1}. {v}")));
    }

    private static List<(string Name, string? Version)> GetPackageReferences(XElement root)
    {
        var packages = new List<(string Name, string? Version)>();

        foreach (var element in root.Descendants().Where(e => e.Name.LocalName == "PackageReference"))
        {
            var name = element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value;

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            // Version can be an attribute or a child element.
            var version = element.Attribute("Version")?.Value
                          ?? element.Elements().FirstOrDefault(e => e.Name.LocalName == "Version")?.Value;

            packages.Add((name, version));
        }

        return packages;
    }

    private static bool UsesCentralPackageManagement(XElement root)
        => root.Descendants()
            .Any(e => e.Name.LocalName == "ManagePackageVersionsCentrally"
                      && string.Equals(e.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase));

    private static void CheckDuplicatePackages(
        List<(string Name, string? Version)> packages,
        List<string> violations)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, _) in packages)
        {
            if (!seen.Add(name))
            {
                violations.Add($"Duplicate PackageReference '{name}'. Remove the redundant entry.");
            }
        }
    }

    private static void CheckFloatingVersions(
        List<(string Name, string? Version)> packages,
        List<string> violations)
    {
        foreach (var (name, version) in packages)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                continue;
            }

            if (version.Contains('*', StringComparison.Ordinal)
                || version.Contains('[', StringComparison.Ordinal)
                || version.Contains('(', StringComparison.Ordinal))
            {
                violations.Add(
                    $"Package '{name}' uses a floating or range version '{version}'. " +
                    "Pin to an exact version for reproducible builds.");
            }
        }
    }

    private static void CheckMissingVersions(
        List<(string Name, string? Version)> packages,
        bool usesCpm,
        List<string> violations)
    {
        if (usesCpm)
        {
            return;
        }

        foreach (var (name, version) in packages)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                violations.Add(
                    $"Package '{name}' has no Version specified. " +
                    "Add an explicit version or enable Central Package Management.");
            }
        }
    }

    private static void CheckBuiltInAlternatives(
        List<(string Name, string? Version)> packages,
        List<string> violations)
    {
        foreach (var (name, _) in packages)
        {
            if (BuiltInAlternatives.TryGetValue(name, out var alternative))
            {
                violations.Add(
                    $"Package '{name}' has a built-in .NET alternative: {alternative}. " +
                    "Consider whether the built-in option meets your needs before keeping this dependency.");
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Tool invoked: {ToolName} | content length: {ContentLength}")]
    private static partial void LogToolInvoked(ILogger logger, string toolName, int contentLength);
}
