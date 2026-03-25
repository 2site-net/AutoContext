namespace SharpPilot.Tools.Checkers.DotNet;

using System.ComponentModel;
using System.Text.Json.Nodes;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using ModelContextProtocol.Server;

using SharpPilot.Tools.Checkers;

/// <summary>
/// Validates that nullable reference type safety is maintained: no #nullable disable
/// directives, and no use of the null-forgiving (!) operator to suppress warnings.
/// </summary>
[McpServerToolType]
public sealed class CSharpNullableContextChecker : IChecker
{
    /// <inheritdoc />
    public string ToolName
        => "check_csharp_nullable_context";

    /// <summary>
    /// Checks C# source code for nullable context violations.
    /// </summary>
    [McpServerTool(Name = "check_csharp_nullable_context", ReadOnly = true, Idempotent = true)]
    [Description(
        "Checks C# source code for nullable context violations: " +
        "#nullable disable directives are not allowed (nullable is expected to be enabled project-wide " +
        "via <Nullable>enable</Nullable> in the .csproj), " +
        "and the null-forgiving operator (!) must not be used to suppress nullable warnings.")]
    public string Check(
        [Description("The C# source code to check.")]
        string content,
        JsonObject? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var tree = CSharpSyntaxTree.ParseText(content);
        var root = tree.GetRoot();
        var violations = new List<string>();

        CheckNullableDisable(root, tree, violations);
        CheckNullForgivingOperator(root, tree, violations);

        return violations.Count == 0
            ? "✅ Nullable context is correct."
            : $"❌ Found {violations.Count} nullable context violation(s):\n" +
              string.Join('\n', violations.Select((v, i) => $"  {i + 1}. {v}"));
    }

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
