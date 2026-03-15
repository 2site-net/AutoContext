namespace QaMcp.Tools.DotNet;

using System.ComponentModel;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using ModelContextProtocol.Server;

/// <summary>
/// Validates C# project structure conventions: file-scoped namespaces,
/// single type per file, file name matches type name, and no #pragma warning disable.
/// </summary>
[McpServerToolType]
public sealed class ProjectStructureChecker : IChecker
{
    /// <inheritdoc />
    public string ToolName
        => "check_project_structure";

    /// <summary>
    /// Checks C# source code for project structure violations.
    /// </summary>
    [McpServerTool(Name = "check_project_structure", ReadOnly = true, Idempotent = true)]
    [Description(
        "Checks C# source code for project structure violations: " +
        "file-scoped namespaces are required (not block-scoped), " +
        "only one top-level type declaration per file is allowed, " +
        "the file name (without extension) must match the type name when provided, " +
        "and #pragma warning disable is not allowed (use [SuppressMessage] with a justification instead).")]
    public string Check(
        [Description("The C# source code to check.")]
        string content,
        [Description("Optional comma-separated metadata. The first segment is the file name " +
            "(e.g., 'MyClass.cs') — when provided, validates that it matches the declared type name.")]
        string? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var fileName = data?.Split(',', 2) is [{ Length: > 0 } f, ..] ? f : null;

        var tree = CSharpSyntaxTree.ParseText(content);
        var root = tree.GetRoot();
        var violations = new List<string>();

        CheckFileScopedNamespace(root, tree, violations);
        CheckSingleTypePerFile(root, violations);
        CheckFileNameMatchesType(root, fileName, violations);
        CheckPragmaWarningDisable(root, tree, violations);

        return violations.Count == 0
            ? "✅ Project structure is correct."
            : $"❌ Found {violations.Count} project structure violation(s):\n" +
              string.Join('\n', violations.Select((v, i) => $"  {i + 1}. {v}"));
    }

    private static void CheckFileScopedNamespace(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        var blockNamespaces = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().ToList();

        foreach (var ns in blockNamespaces)
        {
            var line = tree.GetLineSpan(ns.Span).StartLinePosition.Line + 1;
            violations.Add(
                $"Line {line}: Block-scoped namespace '{ns.Name}' detected. " +
                "Use a file-scoped namespace instead (e.g., 'namespace MyApp.Services;').");
        }
    }

    private static void CheckSingleTypePerFile(SyntaxNode root, List<string> violations)
    {
        var topLevelTypes = CollectTopLevelTypes(root);

        if (topLevelTypes.Count <= 1)
        {
            return;
        }

        var names = topLevelTypes.Select(GetTypeName).ToList();

        violations.Add(
            $"File contains {topLevelTypes.Count} top-level type declarations ({string.Join(", ", names.Select(n => $"'{n}'"))}). " +
            "Keep a single type per file and name the file after that type.");
    }

    private static void CheckFileNameMatchesType(SyntaxNode root, string? fileName, List<string> violations)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var topLevelTypes = CollectTopLevelTypes(root);

        // Only check file name when there is exactly one type; multi-type files
        // are already covered by CheckSingleTypePerFile.
        if (topLevelTypes.Count != 1)
        {
            return;
        }

        var expectedName = Path.GetFileNameWithoutExtension(fileName);
        var actualName = GetTypeName(topLevelTypes[0]);

        if (!string.Equals(expectedName, actualName, StringComparison.Ordinal))
        {
            violations.Add(
                $"File name '{fileName}' does not match the type name '{actualName}'. " +
                $"Rename the file to '{actualName}.cs'.");
        }
    }

    /// <summary>
    /// Collects all top-level type declarations (classes, structs, interfaces, enums, records, delegates)
    /// from file-scoped namespaces, block-scoped namespaces, and the compilation-unit level.
    /// </summary>
    private static List<MemberDeclarationSyntax> CollectTopLevelTypes(SyntaxNode root)
    {
        var types = new List<MemberDeclarationSyntax>();

        foreach (var fileScopedNs in root.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>())
        {
            types.AddRange(fileScopedNs.Members.OfType<BaseTypeDeclarationSyntax>());
            types.AddRange(fileScopedNs.Members.OfType<DelegateDeclarationSyntax>());
        }

        foreach (var blockNs in root.DescendantNodes().OfType<NamespaceDeclarationSyntax>())
        {
            types.AddRange(blockNs.Members.OfType<BaseTypeDeclarationSyntax>());
            types.AddRange(blockNs.Members.OfType<DelegateDeclarationSyntax>());
        }

        if (root is CompilationUnitSyntax compilationUnit)
        {
            types.AddRange(compilationUnit.Members.OfType<BaseTypeDeclarationSyntax>());
            types.AddRange(compilationUnit.Members.OfType<DelegateDeclarationSyntax>());
        }

        return types;
    }

    private static string GetTypeName(MemberDeclarationSyntax member)
        => member switch
        {
            BaseTypeDeclarationSyntax t => t.Identifier.Text,
            DelegateDeclarationSyntax d => d.Identifier.Text,
            _ => string.Empty,
        };

    private static void CheckPragmaWarningDisable(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        foreach (var trivia in root.DescendantTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
            {
                continue;
            }

            if (trivia.GetStructure() is not PragmaWarningDirectiveTriviaSyntax directive)
            {
                continue;
            }

            if (!directive.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
            {
                continue;
            }

            var line = tree.GetLineSpan(trivia.Span).StartLinePosition.Line + 1;
            violations.Add(
                $"Line {line}: '#pragma warning disable' is not preferred. " +
                "Use [SuppressMessage] attribute with a justification instead.");
        }
    }
}
