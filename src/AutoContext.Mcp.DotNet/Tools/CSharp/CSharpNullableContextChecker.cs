namespace AutoContext.Mcp.DotNet.Tools.CSharp;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using AutoContext.Mcp.Shared.Checkers;

/// <summary>
/// Enforces nullable safety rules from <c>lang-csharp.instructions.md</c>:
/// no #nullable disable directives, and no use of the null-forgiving (!) operator
/// to suppress warnings.
/// </summary>
public sealed class CSharpNullableContextChecker : IChecker
{
    /// <inheritdoc />
    public string ToolName
        => "check_csharp_nullable_context";

    /// <summary>
    /// Checks C# source code for nullable context violations.
    /// </summary>
    public async Task<string> CheckAsync(
        string content,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var tree = CSharpSyntaxTree.ParseText(content, cancellationToken: ct);
        var root = await tree.GetRootAsync(ct).ConfigureAwait(false);
        var violations = new List<string>();

        CheckNullableDisable(root, tree, violations);
        CheckNullForgivingOperator(root, tree, violations);

        return violations.Count == 0
            ? "✅ Nullable context is correct."
            : $"❌ Found {violations.Count} nullable context violation(s):\n" +
              string.Join('\n', violations.Select((v, i) => $"  {i + 1}. {v}"));
    }

    // [csharp INST0019]: keep #nullable enable
    private static void CheckNullableDisable(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        foreach (var trivia in root.DescendantTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.NullableDirectiveTrivia))
            {
                continue;
            }

            if (trivia.GetStructure() is not NullableDirectiveTriviaSyntax directive)
            {
                continue;
            }

            if (directive.SettingToken.IsKind(SyntaxKind.DisableKeyword))
            {
                var line = tree.GetLineSpan(trivia.Span).StartLinePosition.Line + 1;
                violations.Add(
                    $"Line {line}: '#nullable disable' is not allowed. " +
                    "Keep nullable reference types enabled globally via <Nullable>enable</Nullable> in the project file.");
            }
        }
    }

    // [csharp INST0020]: no null-forgiving operator (!)
    private static void CheckNullForgivingOperator(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        foreach (var expression in root.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>())
        {
            if (!expression.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            {
                continue;
            }

            var line = tree.GetLineSpan(expression.Span).StartLinePosition.Line + 1;
            violations.Add(
                $"Line {line}: The null-forgiving operator '!' is not allowed. " +
                "Fix the underlying nullability issue instead of suppressing the warning.");
        }
    }
}
