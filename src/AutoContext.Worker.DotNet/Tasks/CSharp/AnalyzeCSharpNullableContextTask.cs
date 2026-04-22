namespace AutoContext.Worker.DotNet.Tasks.CSharp;

using System.Text.Json;
using System.Text.Json.Nodes;

using AutoContext.Mcp;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// <c>analyze_csharp_nullable_context</c> — enforces nullable safety rules
/// from <c>lang-csharp.instructions.md</c>: no <c>#nullable disable</c>
/// directives and no use of the null-forgiving (<c>!</c>) operator to
/// suppress warnings.
/// </summary>
/// <remarks>
/// Request <c>data</c>:  <c>{ "content": "&lt;csharp-source&gt;" }</c><br/>
/// Response <c>output</c>: <c>{ "passed": &lt;bool&gt;, "report": "&lt;text&gt;" }</c>
/// </remarks>
internal sealed class AnalyzeCSharpNullableContextTask : IMcpTask
{
    public string TaskName => "analyze_csharp_nullable_context";

    public async Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken ct)
    {
        if (data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("content", out var contentElement)
            || contentElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("'data.content' is required and must be a string.");
        }

        var content = contentElement.GetString()!;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("'data.content' must not be empty or whitespace.");
        }

        var (passed, report) = await BuildReportAsync(content, ct).ConfigureAwait(false);

        var output = new JsonObject
        {
            ["passed"] = passed,
            ["report"] = report,
        };

        return JsonSerializer.SerializeToElement(output);
    }

    private static async Task<(bool Passed, string Report)> BuildReportAsync(string content, CancellationToken ct)
    {
        var tree = CSharpSyntaxTree.ParseText(content, cancellationToken: ct);
        var root = await tree.GetRootAsync(ct).ConfigureAwait(false);
        var violations = new List<string>();

        AnalyzeNullableDisable(root, tree, violations);
        AnalyzeNullForgivingOperator(root, tree, violations);

        if (violations.Count == 0)
        {
            return (true, "✅ Nullable context is correct.");
        }

        var report = $"❌ Found {violations.Count} nullable context violation(s):\n" +
                     string.Join('\n', violations.Select((v, i) => $"  {i + 1}. {v}"));

        return (false, report);
    }

    // [csharp INST0019]: keep #nullable enable
    private static void AnalyzeNullableDisable(SyntaxNode root, SyntaxTree tree, List<string> violations)
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
    private static void AnalyzeNullForgivingOperator(SyntaxNode root, SyntaxTree tree, List<string> violations)
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
