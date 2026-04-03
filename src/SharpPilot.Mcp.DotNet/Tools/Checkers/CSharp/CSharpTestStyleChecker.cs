namespace SharpPilot.Mcp.DotNet.Tools.Checkers.CSharp;

using System.ComponentModel;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using ModelContextProtocol.Server;

using SharpPilot.Mcp.Shared.Checkers;

/// <summary>
/// Validates xUnit test style conventions: test class naming, test method naming,
/// no XML doc comments, Assert.Multiple for multi-assertion tests, no ConfigureAwait,
/// and structure mirroring (file name, namespace) against the production project.
/// </summary>
[McpServerToolType]
public sealed class CSharpTestStyleChecker : IChecker
{
    /// <inheritdoc />
    public string ToolName
        => "check_csharp_test_style";

    /// <summary>
    /// Checks C# test source code for test style violations.
    /// </summary>
    [McpServerTool(Name = "check_csharp_test_style", ReadOnly = true, Idempotent = true)]
    [Description(
        "Checks C# test source code for test style violations: " +
        "test classes must be suffixed with 'Tests', " +
        "test methods must start with 'Should_' or 'Should_not_', " +
        "no XML doc comments on test classes or test methods, " +
        "Assert.Multiple() is required when a test method has more than one Assert call, " +
        ".ConfigureAwait() must not be called inside test methods (xUnit1030), " +
        "and when data is provided, validates that the test file " +
        "mirrors the production structure (file name ends with 'Tests' before extensions, namespace mirrors production).")]
    public async Task<string> CheckAsync(
        [Description("The C# test source code to check.")]
        string content,
        [Description("Optional metadata. " +
            "'testFileName' (e.g., 'UserServiceTests.cs') validates the name ends with 'Tests' before extensions. " +
            "'productionNamespace' (e.g., 'MyApp.Services') validates the test namespace mirrors it.")]
        IReadOnlyDictionary<string, string>? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var fileName = data?.GetValueOrDefault("testFileName") ?? string.Empty;
        var productionNamespace = data?.GetValueOrDefault("productionNamespace") ?? string.Empty;

        var tree = CSharpSyntaxTree.ParseText(content);
        var root = await tree.GetRootAsync().ConfigureAwait(false);
        var violations = new List<string>();

        var testClasses = FindTestClasses(root);

        if (testClasses.Count == 0)
        {
            return "✅ Test style is correct.";
        }

        CheckFileNameConvention(fileName, violations);
        CheckNamespaceMirroring(root, productionNamespace, violations);

        foreach (var testClass in testClasses)
        {
            CheckTestClassNaming(testClass, tree, violations);
            CheckTestClassXmlDoc(testClass, tree, violations);

            var testMethods = GetTestMethods(testClass);

            foreach (var method in testMethods)
            {
                CheckTestMethodNaming(method, tree, violations);
                CheckTestMethodXmlDoc(method, tree, violations);
                CheckAssertMultiple(method, tree, violations);
                CheckConfigureAwait(method, tree, violations);
            }
        }

        return violations.Count == 0
            ? "✅ Test style is correct."
            : $"❌ Found {violations.Count} test style violation(s):\n" +
              string.Join('\n', violations.Select((v, i) => $"  {i + 1}. {v}"));
    }

    // [testing INST0006]: test file name ends with Tests
    private static void CheckFileNameConvention(ReadOnlySpan<char> fileName, List<string> violations)
    {
        if (fileName.IsEmpty || fileName.IsWhiteSpace())
        {
            return;
        }

        var nameWithoutExtension = StripAllExtensions(fileName);

        if (!nameWithoutExtension.EndsWith("Tests", StringComparison.Ordinal))
        {
            violations.Add(
                $"Test file '{fileName}' must end with 'Tests' before the extension " +
                $"(e.g., '{nameWithoutExtension}Tests.cs').");
        }
    }

    /// <summary>
    /// Strips all extensions from a file name (e.g., "FooTests.razor.cs" → "FooTests").
    /// </summary>
    private static ReadOnlySpan<char> StripAllExtensions(ReadOnlySpan<char> fileName)
    {
        var name = Path.GetFileName(fileName);
        var dotIndex = name.IndexOf('.');

        return dotIndex < 0 ? name : name[..dotIndex];
    }

    // [testing INST0006]: test namespace mirrors production
    private static void CheckNamespaceMirroring(SyntaxNode root, ReadOnlySpan<char> productionNamespace, List<string> violations)
    {
        if (productionNamespace.IsEmpty || productionNamespace.IsWhiteSpace())
        {
            return;
        }

        var declaredNamespace = GetDeclaredNamespace(root);

        if (declaredNamespace is null)
        {
            return;
        }

        // Expected pattern: insert ".Tests" after the first segment of the production namespace.
        // E.g., "MyApp.Services.Users" → "MyApp.Tests.Services.Users"
        ReadOnlySpan<char> declared = declaredNamespace;
        var dotIndex = productionNamespace.IndexOf('.');
        const int testsSuffixLength = 6; // ".Tests".Length
        bool matches;

        if (dotIndex < 0)
        {
            matches = declared.Length == productionNamespace.Length + testsSuffixLength
                      && declared.StartsWith(productionNamespace, StringComparison.Ordinal)
                      && declared[productionNamespace.Length..].Equals(".Tests", StringComparison.Ordinal);
        }
        else
        {
            var first = productionNamespace[..dotIndex];
            var rest = productionNamespace[dotIndex..];

            matches = declared.Length == first.Length + testsSuffixLength + rest.Length
                      && declared.StartsWith(first, StringComparison.Ordinal)
                      && declared[first.Length..].StartsWith(".Tests", StringComparison.Ordinal)
                      && declared[(first.Length + testsSuffixLength)..].Equals(rest, StringComparison.Ordinal);
        }

        if (!matches)
        {
            var expectedNamespace = dotIndex < 0
                ? $"{productionNamespace}.Tests"
                : $"{productionNamespace[..dotIndex]}.Tests{productionNamespace[dotIndex..]}";

            violations.Add(
                $"Test namespace '{declaredNamespace}' does not mirror the production namespace '{productionNamespace}'. " +
                $"Expected '{expectedNamespace}'.");
        }
    }

    private static string? GetDeclaredNamespace(SyntaxNode root)
    {
        var fileScopedNs = root.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();

        if (fileScopedNs is not null)
        {
            return fileScopedNs.Name.ToString();
        }

        var blockNs = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();

        return blockNs?.Name.ToString();
    }

    private static List<TypeDeclarationSyntax> FindTestClasses(SyntaxNode root)
        => [.. root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Where(TestDetection.IsTestClass)];

    private static List<MethodDeclarationSyntax> GetTestMethods(TypeDeclarationSyntax testClass)
        => [.. testClass.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(TestDetection.HasTestAttribute)];

    // [testing INST0006]: test class suffix Tests
    private static void CheckTestClassNaming(TypeDeclarationSyntax testClass, SyntaxTree tree, List<string> violations)
    {
        var name = testClass.Identifier.Text;

        if (!name.EndsWith("Tests", StringComparison.Ordinal))
        {
            var line = tree.GetLineSpan(testClass.Identifier.Span).StartLinePosition.Line + 1;
            violations.Add(
                $"Line {line}: Test class '{name}' must be suffixed with 'Tests' " +
                $"(e.g., '{name}Tests').");
        }
    }

    // [testing INST0019]: no XML docs on test classes
    private static void CheckTestClassXmlDoc(TypeDeclarationSyntax testClass, SyntaxTree tree, List<string> violations)
    {
        if (!HasXmlDocComment(testClass))
        {
            return;
        }

        var line = tree.GetLineSpan(testClass.Identifier.Span).StartLinePosition.Line + 1;
        violations.Add(
            $"Line {line}: Test class '{testClass.Identifier.Text}' should not have XML doc comments. " +
            "Rely on descriptive names to convey intent.");
    }

    // [testing INST0006]: test method prefix Should_ / Should_not_
    private static void CheckTestMethodNaming(MethodDeclarationSyntax method, SyntaxTree tree, List<string> violations)
    {
        var name = method.Identifier.Text;

        if (!name.StartsWith("Should_", StringComparison.Ordinal))
        {
            var line = tree.GetLineSpan(method.Identifier.Span).StartLinePosition.Line + 1;
            violations.Add(
                $"Line {line}: Test method '{name}' must start with 'Should_' or 'Should_not_' " +
                $"(e.g., 'Should_{ToSnakeCase(name)}').");
        }
    }

    // [testing INST0019]: no XML docs on test methods
    private static void CheckTestMethodXmlDoc(MethodDeclarationSyntax method, SyntaxTree tree, List<string> violations)
    {
        if (!HasXmlDocComment(method))
        {
            return;
        }

        var line = tree.GetLineSpan(method.Identifier.Span).StartLinePosition.Line + 1;
        violations.Add(
            $"Line {line}: Test method '{method.Identifier.Text}' should not have XML doc comments. " +
            "Rely on descriptive names to convey intent.");
    }

    // [xunit INST0004]: Assert.Multiple for multiple assertions
    private static void CheckAssertMultiple(MethodDeclarationSyntax method, SyntaxTree tree, List<string> violations)
    {
        if (method.Body is null && method.ExpressionBody is null)
        {
            return;
        }

        var assertCalls = GetAssertCalls(method).ToList();

        if (assertCalls.Count <= 1)
        {
            return;
        }

        // If all Assert calls are already inside an Assert.Multiple lambda, it's fine.
        var hasAssertMultiple = assertCalls.Any(call =>
            call is { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Multiple" } });

        if (hasAssertMultiple)
        {
            return;
        }

        var line = tree.GetLineSpan(method.Identifier.Span).StartLinePosition.Line + 1;
        violations.Add(
            $"Line {line}: Test method '{method.Identifier.Text}' has {assertCalls.Count} Assert calls " +
            "but does not use Assert.Multiple(). Wrap multiple assertions in Assert.Multiple() " +
            "so all failures are reported together.");
    }

    // [xunit INST0009]: no .ConfigureAwait() in test methods
    private static void CheckConfigureAwait(MethodDeclarationSyntax method, SyntaxTree tree, List<string> violations)
    {
        var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            if (!string.Equals(memberAccess.Name.Identifier.Text, "ConfigureAwait", StringComparison.Ordinal))
            {
                continue;
            }

            var line = tree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1;
            violations.Add(
                $"Line {line}: Test method '{method.Identifier.Text}' calls .ConfigureAwait(). " +
                "Do not call .ConfigureAwait() inside test methods (xUnit1030).");
        }
    }

    private static IEnumerable<InvocationExpressionSyntax> GetAssertCalls(MethodDeclarationSyntax method)
        => method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.Text: "Assert" },
            });

    private static bool HasXmlDocComment(SyntaxNode node)
        => node.GetLeadingTrivia()
            .Any(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                      || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var result = new System.Text.StringBuilder();
        result.Append(char.ToLowerInvariant(name[0]));

        for (var i = 1; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]))
            {
                result.Append('_');
                result.Append(char.ToLowerInvariant(name[i]));
            }
            else
            {
                result.Append(name[i]);
            }
        }

        return result.ToString();
    }
}
