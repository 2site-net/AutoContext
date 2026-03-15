namespace QaMcp.Tools.DotNet;

using System.ComponentModel;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using ModelContextProtocol.Server;

/// <summary>
/// Validates C# naming conventions: interface I prefix, extension class Extensions suffix,
/// async method Async suffix, private instance field _camelCase, PascalCase for
/// types/methods/properties/events, and camelCase for parameters.
/// </summary>
[McpServerToolType]
public sealed class NamingConventionsChecker : IChecker
{
    /// <inheritdoc />
    public string ToolName
        => "check_naming_conventions";

    /// <summary>
    /// Checks C# source code for naming convention violations.
    /// </summary>
    [McpServerTool(Name = "check_naming_conventions", ReadOnly = true, Idempotent = true)]
    [Description(
        "Checks C# source code for naming convention violations: " +
        "interfaces must be prefixed with 'I' followed by an uppercase letter (e.g., IMyType), " +
        "extension classes must be suffixed with 'Extensions', " +
        "async methods (except overrides, event handlers, and test methods) must be suffixed with 'Async', " +
        "private non-static instance fields must use _camelCase, " +
        "types, methods, properties, and events must use PascalCase, " +
        "and method/constructor/delegate parameters must use camelCase.")]
    public string Check(
        [Description("The C# source code to check.")]
        string content,
        string? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var tree = CSharpSyntaxTree.ParseText(content);
        var root = tree.GetRoot();
        var violations = new List<string>();

        CheckInterfaceNames(root, tree, violations);
        CheckExtensionClassNames(root, tree, violations);
        CheckAsyncMethodNames(root, tree, violations);
        CheckPrivateFieldNames(root, tree, violations);
        CheckPascalCaseTypes(root, tree, violations);
        CheckPascalCaseMethods(root, tree, violations);
        CheckPascalCaseProperties(root, tree, violations);
        CheckPascalCaseEvents(root, tree, violations);
        CheckCamelCaseParameters(root, tree, violations);

        return violations.Count == 0
            ? "✅ Naming conventions are correct."
            : $"❌ Found {violations.Count} naming violation(s):\n" +
              string.Join('\n', violations.Select((v, i) => $"  {i + 1}. {v}"));
    }

    private static void CheckInterfaceNames(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        foreach (var iface in root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
        {
            var name = iface.Identifier.Text;

            if (!IsValidInterfaceName(name))
            {
                var line = tree.GetLineSpan(iface.Span).StartLinePosition.Line + 1;
                violations.Add(
                    $"Line {line}: Interface '{name}' must be prefixed with 'I' followed by an uppercase letter " +
                    $"(e.g., 'I{name}').");
            }
        }
    }

    private static bool IsValidInterfaceName(string name)
        => name.Length >= 2 && name[0] == 'I' && char.IsUpper(name[1]);

    private static void CheckExtensionClassNames(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (!classDecl.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                continue;
            }

            var hasExtensionMethod = classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .Any(IsExtensionMethod);

            if (!hasExtensionMethod)
            {
                continue;
            }

            var name = classDecl.Identifier.Text;

            if (!name.EndsWith("Extensions", StringComparison.Ordinal))
            {
                var line = tree.GetLineSpan(classDecl.Span).StartLinePosition.Line + 1;
                violations.Add(
                    $"Line {line}: Extension class '{name}' must be suffixed with 'Extensions' " +
                    $"(e.g., '{name}Extensions').");
            }
        }
    }

    private static bool IsExtensionMethod(MethodDeclarationSyntax method)
        => method.Modifiers.Any(SyntaxKind.StaticKeyword)
           && method.ParameterList.Parameters.Count > 0
           && method.ParameterList.Parameters[0].Modifiers.Any(SyntaxKind.ThisKeyword);

    private static void CheckAsyncMethodNames(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword))
            {
                continue;
            }

            if (method.Modifiers.Any(SyntaxKind.OverrideKeyword))
            {
                continue;
            }

            if (TestDetection.HasTestAttribute(method))
            {
                continue;
            }

            if (TestDetection.IsLikelyEventHandler(method))
            {
                continue;
            }

            var name = method.Identifier.Text;

            if (!name.EndsWith("Async", StringComparison.Ordinal))
            {
                var line = tree.GetLineSpan(method.Span).StartLinePosition.Line + 1;
                violations.Add(
                    $"Line {line}: Async method '{name}' must be suffixed with 'Async' " +
                    $"(e.g., '{name}Async').");
            }
        }
    }

    private static void CheckPrivateFieldNames(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            // Constants use PascalCase and static fields may follow different conventions — skip both.
            if (field.Modifiers.Any(SyntaxKind.ConstKeyword)
                || field.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                continue;
            }

            if (!IsPrivateOrDefault(field.Modifiers))
            {
                continue;
            }

            foreach (var variable in field.Declaration.Variables)
            {
                var name = variable.Identifier.Text;

                if (!IsValidPrivateFieldName(name))
                {
                    var line = tree.GetLineSpan(variable.Span).StartLinePosition.Line + 1;
                    violations.Add(
                        $"Line {line}: Private field '{name}' must use _camelCase naming " +
                        "(start with an underscore followed by a lowercase letter, e.g., '_myField').");
                }
            }
        }
    }

    private static bool IsValidPrivateFieldName(string name)
        => name.Length >= 2 && name[0] == '_' && char.IsLower(name[1]);

    private static bool IsPrivateOrDefault(SyntaxTokenList modifiers)
    {
        var hasPublic = modifiers.Any(SyntaxKind.PublicKeyword);
        var hasInternal = modifiers.Any(SyntaxKind.InternalKeyword);
        var hasProtected = modifiers.Any(SyntaxKind.ProtectedKeyword);

        return !hasPublic && !hasInternal && !hasProtected;
    }

    private static void CheckPascalCaseTypes(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        foreach (var typeDecl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            var name = typeDecl.Identifier.Text;

            if (!IsPascalCase(name))
            {
                var line = tree.GetLineSpan(typeDecl.Span).StartLinePosition.Line + 1;
                violations.Add(
                    $"Line {line}: Type '{name}' must use PascalCase naming (start with an uppercase letter).");
            }
        }

        foreach (var delegateDecl in root.DescendantNodes().OfType<DelegateDeclarationSyntax>())
        {
            var name = delegateDecl.Identifier.Text;

            if (!IsPascalCase(name))
            {
                var line = tree.GetLineSpan(delegateDecl.Span).StartLinePosition.Line + 1;
                violations.Add(
                    $"Line {line}: Delegate '{name}' must use PascalCase naming (start with an uppercase letter).");
            }
        }
    }

    private static void CheckPascalCaseMethods(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var isTestClass = TestDetection.IsTestClass(typeDecl);

            foreach (var method in typeDecl.Members.OfType<MethodDeclarationSyntax>())
            {
                // Test method names follow Should_do_something convention — skip PascalCase check.
                if (isTestClass && TestDetection.HasTestAttribute(method))
                {
                    continue;
                }

                var name = method.Identifier.Text;

                if (!IsPascalCase(name))
                {
                    var line = tree.GetLineSpan(method.Span).StartLinePosition.Line + 1;
                    violations.Add(
                        $"Line {line}: Method '{name}' must use PascalCase naming (start with an uppercase letter).");
                }
            }
        }
    }

    private static void CheckPascalCaseProperties(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            var name = property.Identifier.Text;

            if (!IsPascalCase(name))
            {
                var line = tree.GetLineSpan(property.Span).StartLinePosition.Line + 1;
                violations.Add(
                    $"Line {line}: Property '{name}' must use PascalCase naming (start with an uppercase letter).");
            }
        }
    }

    private static void CheckPascalCaseEvents(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        foreach (var eventDecl in root.DescendantNodes().OfType<EventDeclarationSyntax>())
        {
            var name = eventDecl.Identifier.Text;

            if (!IsPascalCase(name))
            {
                var line = tree.GetLineSpan(eventDecl.Span).StartLinePosition.Line + 1;
                violations.Add(
                    $"Line {line}: Event '{name}' must use PascalCase naming (start with an uppercase letter).");
            }
        }

        foreach (var eventField in root.DescendantNodes().OfType<EventFieldDeclarationSyntax>())
        {
            foreach (var variable in eventField.Declaration.Variables)
            {
                var name = variable.Identifier.Text;

                if (!IsPascalCase(name))
                {
                    var line = tree.GetLineSpan(variable.Span).StartLinePosition.Line + 1;
                    violations.Add(
                        $"Line {line}: Event '{name}' must use PascalCase naming (start with an uppercase letter).");
                }
            }
        }
    }

    private static void CheckCamelCaseParameters(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        foreach (var parameter in root.DescendantNodes().OfType<ParameterSyntax>())
        {
            // Only check method, constructor, delegate, and indexer parameters — not lambda params.
            if (parameter.Parent?.Parent is not (MethodDeclarationSyntax
                or ConstructorDeclarationSyntax
                or DelegateDeclarationSyntax
                or IndexerDeclarationSyntax
                or LocalFunctionStatementSyntax))
            {
                continue;
            }

            // 'this' parameters in extension methods follow their own conventions.
            if (parameter.Modifiers.Any(SyntaxKind.ThisKeyword))
            {
                continue;
            }

            var name = parameter.Identifier.Text;

            // Skip discards.
            if (string.IsNullOrEmpty(name) || name == "_")
            {
                continue;
            }

            if (!IsCamelCase(name))
            {
                var line = tree.GetLineSpan(parameter.Span).StartLinePosition.Line + 1;
                violations.Add(
                    $"Line {line}: Parameter '{name}' must use camelCase naming " +
                    "(start with a lowercase letter, no leading underscore).");
            }
        }
    }

    private static bool IsPascalCase(string name)
        => name.Length > 0 && char.IsUpper(name[0]);

    private static bool IsCamelCase(string name)
        => name.Length > 0 && char.IsLower(name[0]);
}
