namespace SharpPilot.Mcp.DotNet.Tools.Checkers.DotNet.CSharp;

using System.ComponentModel;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using ModelContextProtocol.Server;

using SharpPilot.Mcp.DotNet.Tools.Checkers;

/// <summary>
/// Validates C# async/await patterns: no async void (except event handlers),
/// public async APIs require a CancellationToken parameter, and all await
/// expressions in non-test code must use .ConfigureAwait(false).
/// </summary>
[McpServerToolType]
public sealed class CSharpAsyncPatternChecker : IChecker
{
    /// <inheritdoc />
    public string ToolName
        => "check_csharp_async_patterns";

    /// <summary>
    /// Checks C# source code for async/await pattern violations.
    /// </summary>
    [McpServerTool(Name = "check_csharp_async_patterns", ReadOnly = true, Idempotent = true)]
    [Description(
        "Checks C# source code for async/await pattern violations: " +
        "async void is not allowed except for event handlers (two-parameter methods where the last parameter type contains 'EventArgs'), " +
        "public async methods (non-void, non-override) must include a CancellationToken parameter, " +
        "and all await expressions in non-test code must use .ConfigureAwait(false).")]
    public string Check(
        [Description("The C# source code to check.")]
        string content,
        IReadOnlyDictionary<string, string>? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var tree = CSharpSyntaxTree.ParseText(content);
        var root = tree.GetRoot();
        var violations = new List<string>();

        CheckAsyncVoid(root, tree, violations);
        CheckCancellationToken(root, tree, violations);
        CheckConfigureAwait(root, tree, violations);

        return violations.Count == 0
            ? "✅ Async patterns are correct."
            : $"❌ Found {violations.Count} async pattern violation(s):\n" +
              string.Join('\n', violations.Select((v, i) => $"  {i + 1}. {v}"));
    }

    private static void CheckAsyncVoid(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword))
            {
                continue;
            }

            if (method.ReturnType is not PredefinedTypeSyntax returnType
                || !returnType.Keyword.IsKind(SyntaxKind.VoidKeyword))
            {
                continue;
            }

            if (TestDetection.IsLikelyEventHandler(method))
            {
                continue;
            }

            var line = tree.GetLineSpan(method.Span).StartLinePosition.Line + 1;
            violations.Add(
                $"Line {line}: Method '{method.Identifier.Text}' uses 'async void', which is not allowed. " +
                "Use 'async Task' instead — 'async void' swallows unhandled exceptions.");
        }
    }

    private static void CheckCancellationToken(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword)
                || !method.Modifiers.Any(SyntaxKind.PublicKeyword))
            {
                continue;
            }

            if (method.Modifiers.Any(SyntaxKind.OverrideKeyword))
            {
                continue;
            }

            // Event handlers (async void) do not take a CancellationToken.
            if (method.ReturnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword })
            {
                continue;
            }

            var containingType = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();

            if (containingType is not null && TestDetection.IsTestClass(containingType))
            {
                continue;
            }

            if (HasCancellationTokenParameter(method))
            {
                continue;
            }

            var line = tree.GetLineSpan(method.Span).StartLinePosition.Line + 1;
            violations.Add(
                $"Line {line}: Public async method '{method.Identifier.Text}' is missing a CancellationToken parameter. " +
                "Add 'CancellationToken ct = default' as the last parameter.");
        }
    }

    private static bool HasCancellationTokenParameter(MethodDeclarationSyntax method)
        => method.ParameterList.Parameters
            .Any(p => p.Type is not null && GetSimpleTypeName(p.Type) == "CancellationToken");

    private static string GetSimpleTypeName(TypeSyntax type)
        => type switch
        {
            SimpleNameSyntax simple => simple.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            NullableTypeSyntax nullable => GetSimpleTypeName(nullable.ElementType),
            _ => type.ToString(),
        };

    private static void CheckConfigureAwait(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        foreach (var awaitExpr in root.DescendantNodes().OfType<AwaitExpressionSyntax>())
        {
            var containingType = awaitExpr.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();

            if (containingType is not null && TestDetection.IsTestClass(containingType))
            {
                continue;
            }

            if (HasConfigureAwaitFalse(awaitExpr.Expression))
            {
                continue;
            }

            var line = tree.GetLineSpan(awaitExpr.Span).StartLinePosition.Line + 1;
            violations.Add(
                $"Line {line}: Awaited expression is missing '.ConfigureAwait(false)'. " +
                "Use 'await someTask.ConfigureAwait(false)' in non-test code.");
        }
    }

    private static bool HasConfigureAwaitFalse(ExpressionSyntax expression)
    {
        if (expression is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        if (memberAccess.Name.Identifier.Text != "ConfigureAwait")
        {
            return false;
        }

        var args = invocation.ArgumentList.Arguments;

        if (args.Count != 1)
        {
            return false;
        }

        return args[0].Expression.IsKind(SyntaxKind.FalseLiteralExpression);
    }
}
