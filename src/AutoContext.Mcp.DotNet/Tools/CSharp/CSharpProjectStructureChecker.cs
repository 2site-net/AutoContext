namespace AutoContext.Mcp.DotNet.Tools.CSharp;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using AutoContext.Mcp.Shared.Checkers;

/// <summary>
/// Enforces project structure rules from <c>dotnet-coding-standards.instructions.md</c>:
/// file-scoped namespaces, single type per file, file name matches type name,
/// and no #pragma warning disable.
/// </summary>
public sealed class CSharpProjectStructureChecker : IChecker, IEditorConfigFilter
{
    /// <inheritdoc />
    public string ToolName
        => "check_csharp_project_structure";

    /// <inheritdoc />
    public IReadOnlyList<string> EditorConfigKeys
        => ["csharp_style_namespace_declarations"];

    /// <summary>
    /// Checks C# source code for project structure violations.
    /// </summary>
    public async Task<string> CheckAsync(
        string content,
        IReadOnlyDictionary<string, string>? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var fileName = data?.GetValueOrDefault("originalFileName") ?? string.Empty;
        var disabled = data?.ContainsKey("__disabled") == true;

        var tree = CSharpSyntaxTree.ParseText(content);
        var root = await tree.GetRootAsync().ConfigureAwait(false);
        var violations = new List<string>();

        // EditorConfig-backed: run when EC key is present, or when tool is enabled (use default).
        var namespacePreference = data?.GetValueOrDefault("csharp_style_namespace_declarations");

        if (namespacePreference is not null || !disabled)
        {
            var effective = namespacePreference ?? "file_scoped";

            if (effective is "block_scoped")
            {
                CheckBlockScopedNamespace(root, tree, violations);
            }
            else
            {
                CheckFileScopedNamespace(root, tree, violations);
            }
        }

        if (!disabled)
        {
            CheckSingleTypePerFile(root, violations);
            CheckFileNameMatchesType(root, fileName, violations);
            CheckPragmaWarningDisable(root, tree, violations);
        }

        return violations.Count == 0
            ? "✅ Project structure is correct."
            : $"❌ Found {violations.Count} project structure violation(s):\n" +
              string.Join('\n', violations.Select((v, i) => $"  {i + 1}. {v}"));
    }

    // EditorConfig: csharp_style_namespace_declarations (block-scoped)
    private static void CheckBlockScopedNamespace(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        var fileScopedNamespaces = root.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>().ToList();

        foreach (var ns in fileScopedNamespaces)
        {
            var line = tree.GetLineSpan(ns.Span).StartLinePosition.Line + 1;
            violations.Add(
                $"Line {line}: File-scoped namespace '{ns.Name}' detected. " +
                "Use a block-scoped namespace instead (csharp_style_namespace_declarations = block_scoped).");
        }
    }

    // EditorConfig: csharp_style_namespace_declarations (file-scoped)
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

    // [coding-standards INST0016]: single type per file, file name matches type
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

    // [coding-standards INST0016]: file name matches type name
    private static void CheckFileNameMatchesType(SyntaxNode root, ReadOnlySpan<char> fileName, List<string> violations)
    {
        if (fileName.IsEmpty || fileName.IsWhiteSpace())
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

        if (!expectedName.Equals(actualName, StringComparison.Ordinal))
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

    // [csharp INST0013]: no #pragma warning disable
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
