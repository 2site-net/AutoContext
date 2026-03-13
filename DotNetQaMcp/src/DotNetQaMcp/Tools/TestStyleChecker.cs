namespace DotNetQaMcp.Tools;

using System.ComponentModel;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using ModelContextProtocol.Server;

/// <summary>
/// Validates xUnit test style conventions: test class naming, test method naming,
/// no XML doc comments, Assert.Multiple for multi-assertion tests, no ConfigureAwait,
/// and structure mirroring (file name, namespace) against the production project.
/// </summary>
[McpServerToolType]
public static class TestStyleChecker
{
    private static readonly HashSet<string> TestAttributes = new(StringComparer.Ordinal)
    {
        "Fact", "FactAttribute",
        "Theory", "TheoryAttribute",
        "Test", "TestAttribute",
        "TestCase", "TestCaseAttribute",
    };

    /// <summary>
    /// Checks C# test source code for test style violations.
    /// </summary>
    [McpServerTool(Name = "check_tests_style", ReadOnly = true, Idempotent = true)]
    [Description(
        "Checks C# test source code for test style violations: " +
        "test classes must be suffixed with 'Tests', " +
        "test methods must start with 'Should_' or 'Should_not_', " +
        "no XML doc comments on test classes or test methods, " +
        "Assert.Multiple() is required when a test method has more than one Assert call, " +
        ".ConfigureAwait() must not be called inside test methods (xUnit1030), " +
        "and when fileName/productionNamespace are provided, validates that the test file " +
        "mirrors the production structure (file name ends with 'Tests' before extensions, namespace mirrors production).")]
    public static string Check(
        [Description("The C# test source code to check.")]
        string sourceCode,
        [Description("The test file name (e.g., 'UserServiceTests.cs'). When provided, validates the name (without extensions) ends with 'Tests'. Supports .cs, .razor, and .razor.cs files.")]
        string? fileName = null,
        [Description("The production namespace to mirror (e.g., 'MyApp.Services'). When provided, validates the test namespace mirrors it with a '.Tests' segment inserted.")]
        string? productionNamespace = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCode);

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();
        var violations = new List<string>();

        var testClasses = FindTestClasses(root);

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

    private static void CheckFileNameConvention(string? fileName, List<string> violations)
    {
        if (string.IsNullOrWhiteSpace(fileName))
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
    private static string StripAllExtensions(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var dotIndex = name.IndexOf('.', StringComparison.Ordinal);

        return dotIndex < 0 ? name : name[..dotIndex];
    }

    private static void CheckNamespaceMirroring(SyntaxNode root, string? productionNamespace, List<string> violations)
    {
        if (string.IsNullOrWhiteSpace(productionNamespace))
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
        var dotIndex = productionNamespace.IndexOf('.', StringComparison.Ordinal);
        var expectedNamespace = dotIndex < 0
            ? $"{productionNamespace}.Tests"
            : $"{productionNamespace[..dotIndex]}.Tests{productionNamespace[dotIndex..]}";

        if (!string.Equals(declaredNamespace, expectedNamespace, StringComparison.Ordinal))
        {
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
            .Where(IsTestClass)];

    private static bool IsTestClass(TypeDeclarationSyntax typeDecl)
        => typeDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Any(HasTestAttribute);

    private static List<MethodDeclarationSyntax> GetTestMethods(TypeDeclarationSyntax testClass)
        => [.. testClass.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(HasTestAttribute)];

    private static bool HasTestAttribute(MethodDeclarationSyntax method)
        => method.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => TestAttributes.Contains(GetAttributeName(a)));

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

    private static string GetAttributeName(AttributeSyntax attr)
        => attr.Name switch
        {
            SimpleNameSyntax simple => simple.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            _ => string.Empty,
        };

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
