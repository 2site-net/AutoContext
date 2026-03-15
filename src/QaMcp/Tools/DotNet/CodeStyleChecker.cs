namespace QaMcp.Tools.DotNet;

using System.ComponentModel;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using ModelContextProtocol.Server;

/// <summary>
/// Validates C# code style rules: regions, decorative comments, curly braces,
/// blank lines before control flow, expression-body arrow placement,
/// and XML doc comments on public/protected members.
/// </summary>
[McpServerToolType]
public sealed partial class CodeStyleChecker : IChecker
{
    /// <inheritdoc />
    public string ToolName
        => "check_coding_style";

    /// <summary>
    /// Checks C# source code for code-style violations.
    /// </summary>
    [McpServerTool(Name = "check_coding_style", ReadOnly = true, Idempotent = true)]
    [Description(
        "Checks C# source code for style violations: " +
        "no #region, no decorative section-header comments, " +
        "curly braces required on control flow (except single-line guard clauses), " +
        "blank lines before control flow statements, " +
        "expression-body arrows (=>) must be on the next line, " +
        "and XML doc comments required on public/protected members.")]
    public string Check(
        [Description("The C# source code to check.")]
        string content,
        string? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var tree = CSharpSyntaxTree.ParseText(content);
        var root = tree.GetRoot();
        var normalized = content.ReplaceLineEndings("\n");
        ReadOnlySpan<char> contentSpan = normalized;
        var lineCount = contentSpan.Count('\n') + 1;
        var lineRanges = new Range[lineCount];
        contentSpan.Split(lineRanges, '\n');
        var violations = new List<string>();

        CheckRegions(root, tree, violations);
        CheckDecorativeComments(contentSpan, lineRanges, violations);
        CheckCurlyBraces(root, tree, violations);
        CheckBlankLineBeforeControlFlow(root, tree, contentSpan, lineRanges, violations);
        CheckExpressionBodyArrowPlacement(root, tree, violations);
        CheckXmlDocComments(root, tree, violations);

        return violations.Count == 0
            ? "✅ Code style is correct."
            : $"❌ Found {violations.Count} style violation(s):\n" +
              string.Join('\n', violations.Select((v, i) => $"  {i + 1}. {v}"));
    }

    private static void CheckRegions(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        foreach (var trivia in root.DescendantTrivia())
        {
            if (trivia.IsKind(SyntaxKind.RegionDirectiveTrivia))
            {
                var line = tree.GetLineSpan(trivia.Span).StartLinePosition.Line + 1;
                violations.Add($"Line {line}: #region directives are not allowed. They hide code structure.");
            }
        }
    }

    private static void CheckDecorativeComments(
        ReadOnlySpan<char> content,
        ReadOnlySpan<Range> lineRanges,
        List<string> violations)
    {
        for (var i = 0; i < lineRanges.Length; i++)
        {
            if (DecorativeCommentRegex().IsMatch(content[lineRanges[i]]))
            {
                violations.Add(
                    $"Line {i + 1}: Decorative section-header comment detected. " +
                    "Organize code through consistent member ordering instead.");
            }
        }
    }

    private static void CheckCurlyBraces(SyntaxNode root, SyntaxTree tree, List<string> violations)
    {
        var controlFlowStatements = root.DescendantNodes()
            .Where(n => n is IfStatementSyntax or ElseClauseSyntax
                        or ForStatementSyntax or ForEachStatementSyntax
                        or WhileStatementSyntax or DoStatementSyntax
                        or UsingStatementSyntax or LockStatementSyntax
                        or FixedStatementSyntax);

        foreach (var node in controlFlowStatements)
        {
            var embedded = GetEmbeddedStatement(node);

            if (embedded is null or BlockSyntax)
            {
                continue;
            }

            if (IsGuardClause(node, embedded))
            {
                continue;
            }

            var line = tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
            var keyword = GetControlFlowKeyword(node);
            violations.Add(
                $"Line {line}: '{keyword}' statement requires curly braces " +
                "(exception: single-line guard clauses).");
        }
    }

    private static void CheckBlankLineBeforeControlFlow(
        SyntaxNode root,
        SyntaxTree tree,
        ReadOnlySpan<char> content,
        ReadOnlySpan<Range> lineRanges,
        List<string> violations)
    {
        var controlFlowNodes = root.DescendantNodes()
            .Where(n => n is IfStatementSyntax or ForStatementSyntax
                        or ForEachStatementSyntax or WhileStatementSyntax
                        or SwitchStatementSyntax or DoStatementSyntax
                        or TryStatementSyntax or UsingStatementSyntax
                        or LockStatementSyntax);

        foreach (var node in controlFlowNodes)
        {
            // Skip nodes nested inside another control-flow (else-if chains, etc.)
            if (node.Parent is ElseClauseSyntax or IfStatementSyntax)
            {
                continue;
            }

            var lineIndex = tree.GetLineSpan(node.Span).StartLinePosition.Line;

            if (lineIndex < 1)
            {
                continue;
            }

            // Skip if this is the first statement in a block
            if (IsFirstStatementInBlock(node))
            {
                continue;
            }

            var previousLine = content[lineRanges[lineIndex - 1]].Trim();

            if (previousLine.Length > 0 && !(previousLine.Length == 1 && previousLine[0] == '{'))
            {
                violations.Add(
                    $"Line {lineIndex + 1}: Missing blank line before " +
                    $"'{GetControlFlowKeyword(node)}' statement.");
            }
        }
    }

    private static void CheckExpressionBodyArrowPlacement(
        SyntaxNode root,
        SyntaxTree tree,
        List<string> violations)
    {
        var arrowClauses = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>();

        foreach (var arrow in arrowClauses)
        {
            // Only check method/property/indexer/operator declarations — skip lambdas
            if (arrow.Parent is not (MethodDeclarationSyntax
                or PropertyDeclarationSyntax
                or IndexerDeclarationSyntax
                or OperatorDeclarationSyntax
                or ConversionOperatorDeclarationSyntax
                or LocalFunctionStatementSyntax))
            {
                continue;
            }

            var arrowToken = arrow.ArrowToken;
            var arrowLine = tree.GetLineSpan(arrowToken.Span).StartLinePosition.Line;
            var parentStartLine = tree.GetLineSpan(arrow.Parent!.Span).StartLinePosition.Line;

            if (arrowLine == parentStartLine)
            {
                var line = arrowLine + 1;
                violations.Add(
                    $"Line {line}: Expression-body arrow (=>) must be on the next line, " +
                    "not at the end of the signature.");
            }
        }
    }

    private static StatementSyntax? GetEmbeddedStatement(SyntaxNode node) =>
        node switch
        {
            IfStatementSyntax ifs => ifs.Statement,
            ElseClauseSyntax els => els.Statement,
            ForStatementSyntax fors => fors.Statement,
            ForEachStatementSyntax foreachs => foreachs.Statement,
            WhileStatementSyntax whiles => whiles.Statement,
            DoStatementSyntax dos => dos.Statement,
            UsingStatementSyntax usings => usings.Statement,
            LockStatementSyntax locks => locks.Statement,
            FixedStatementSyntax fixeds => fixeds.Statement,
            _ => null,
        };

    private static bool IsGuardClause(SyntaxNode controlFlowNode, StatementSyntax embedded)
    {
        // A guard clause is a single-line if with return/throw/continue/break and no else
        if (controlFlowNode is not IfStatementSyntax ifStmt)
        {
            return false;
        }

        if (ifStmt.Else is not null)
        {
            return false;
        }

        return embedded is ReturnStatementSyntax
               or ThrowStatementSyntax
               or ContinueStatementSyntax
               or BreakStatementSyntax;
    }

    private static string GetControlFlowKeyword(SyntaxNode node) =>
        node switch
        {
            IfStatementSyntax => "if",
            ElseClauseSyntax => "else",
            ForStatementSyntax => "for",
            ForEachStatementSyntax => "foreach",
            WhileStatementSyntax => "while",
            DoStatementSyntax => "do",
            SwitchStatementSyntax => "switch",
            TryStatementSyntax => "try",
            UsingStatementSyntax => "using",
            LockStatementSyntax => "lock",
            FixedStatementSyntax => "fixed",
            _ => "control flow",
        };

    private static bool IsFirstStatementInBlock(SyntaxNode node)
    {
        if (node.Parent is not BlockSyntax block)
        {
            return false;
        }

        return block.Statements.FirstOrDefault() == node;
    }

    private static void CheckXmlDocComments(
        SyntaxNode root,
        SyntaxTree tree,
        List<string> violations)
    {
        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (TestDetection.IsTestClass(typeDecl))
            {
                continue;
            }

            if (IsPublicOrProtected(typeDecl))
            {
                CheckMemberXmlDoc(typeDecl, tree, violations);
            }

            foreach (var member in typeDecl.Members)
            {
                if (!IsPublicOrProtected(member))
                {
                    continue;
                }

                if (member is MethodDeclarationSyntax method
                    && method.Modifiers.Any(SyntaxKind.OverrideKeyword))
                {
                    continue;
                }

                // Skip nested types — they are checked as top-level type declarations
                if (member is TypeDeclarationSyntax)
                {
                    continue;
                }

                CheckMemberXmlDoc(member, tree, violations);
            }
        }

        // Also check top-level enums and delegates
        foreach (var enumDecl in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
        {
            if (IsPublicOrProtected(enumDecl))
            {
                CheckMemberXmlDoc(enumDecl, tree, violations);
            }
        }

        foreach (var delegateDecl in root.DescendantNodes().OfType<DelegateDeclarationSyntax>())
        {
            if (IsPublicOrProtected(delegateDecl))
            {
                CheckMemberXmlDoc(delegateDecl, tree, violations);
            }
        }
    }

    private static void CheckMemberXmlDoc(
        MemberDeclarationSyntax member,
        SyntaxTree tree,
        List<string> violations)
    {
        var hasXmlDoc = member.GetLeadingTrivia()
            .Any(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));

        if (hasXmlDoc)
        {
            return;
        }

        var line = tree.GetLineSpan(member.Span).StartLinePosition.Line + 1;
        var name = GetMemberDisplayName(member);
        violations.Add($"Line {line}: Public/protected member '{name}' is missing XML doc comment (/// <summary>).");
    }

    private static bool IsPublicOrProtected(MemberDeclarationSyntax member)
    {
        var modifiers = member.Modifiers;

        return modifiers.Any(SyntaxKind.PublicKeyword)
               || modifiers.Any(SyntaxKind.ProtectedKeyword);
    }

    private static string GetMemberDisplayName(MemberDeclarationSyntax member)
        => member switch
        {
            TypeDeclarationSyntax t => t.Identifier.Text,
            EnumDeclarationSyntax e => e.Identifier.Text,
            DelegateDeclarationSyntax d => d.Identifier.Text,
            MethodDeclarationSyntax m => m.Identifier.Text,
            PropertyDeclarationSyntax p => p.Identifier.Text,
            ConstructorDeclarationSyntax c => c.Identifier.Text,
            IndexerDeclarationSyntax => "this[]",
            EventDeclarationSyntax ev => ev.Identifier.Text,
            EventFieldDeclarationSyntax ef
                => ef.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "(event)",
            OperatorDeclarationSyntax op => $"operator {op.OperatorToken.Text}",
            ConversionOperatorDeclarationSyntax co => $"operator {co.Type}",
            FieldDeclarationSyntax f
                => f.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "(field)",
            _ => "(unknown)",
        };

    [GeneratedRegex(
        @"^\s*//\s*[─═━—–\-_]{2,}\s*\S+.*[─═━—–\-_]{2,}|^\s*//\s*[─═━—–\-_]{3,}",
        RegexOptions.CultureInvariant)]
    private static partial Regex DecorativeCommentRegex();
}
