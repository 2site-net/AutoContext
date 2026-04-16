namespace AutoContext.Mcp.DotNet.Tools.CSharp;

using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using AutoContext.Mcp.Shared.Checkers;

/// <summary>
/// Enforces code style rules from <c>lang-csharp.instructions.md</c>:
/// regions, decorative comments, curly braces, blank lines before control flow,
/// expression-body arrow placement, XML doc comments on public/protected members,
/// System-directive ordering, and expression-body style for methods and properties.
/// </summary>
public sealed partial class CSharpCodingStyleChecker : IChecker, IEditorConfigFilter
{
    /// <inheritdoc />
    public string ToolName
        => "check_csharp_coding_style";

    /// <inheritdoc />
    public IReadOnlyList<string> EditorConfigKeys
        => [
            "csharp_prefer_braces",
            "dotnet_sort_system_directives_first",
            "csharp_style_expression_bodied_methods",
            "csharp_style_expression_bodied_properties",
        ];

    /// <summary>
    /// Checks C# source code for code-style violations.
    /// </summary>
    public async Task<string> CheckAsync(
        string content,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var tree = CSharpSyntaxTree.ParseText(content, cancellationToken: ct);
        var root = await tree.GetRootAsync(ct).ConfigureAwait(false);
        var normalized = content.ReplaceLineEndings("\n");
        ReadOnlySpan<char> contentSpan = normalized;
        var lineCount = contentSpan.Count('\n') + 1;
        var lineRanges = new Range[lineCount];
        contentSpan.Split(lineRanges, '\n');
        var violations = new List<string>();
        var disabled = IsDisabled(data);

        if (!disabled)
        {
            CheckRegions(root, tree, violations);
            CheckDecorativeComments(contentSpan, lineRanges, violations);
            CheckBlankLineBeforeControlFlow(root, tree, contentSpan, lineRanges, violations);
            CheckExpressionBodyArrowPlacement(root, tree, violations);
            CheckXmlDocComments(root, tree, violations);
        }

        // EditorConfig-backed checks run when the EC key is present,
        // regardless of whether the tool is disabled.
        var bracePreference = GetBracePreference(data, disabled);

        if (bracePreference is not null)
        {
            CheckCurlyBraces(root, tree, bracePreference, violations);
        }

        var sortSystemFirst = GetSortSystemDirectivesFirst(data, disabled);

        if (sortSystemFirst is not null)
        {
            CheckSortSystemDirectivesFirst(root, tree, sortSystemFirst.Value, violations);
        }

        var expressionBodiedMethods = GetExpressionBodiedPreference(data, "csharp_style_expression_bodied_methods");

        if (expressionBodiedMethods is not null)
        {
            CheckExpressionBodiedMethods(root, tree, expressionBodiedMethods, violations);
        }

        var expressionBodiedProperties = GetExpressionBodiedPreference(data, "csharp_style_expression_bodied_properties");

        if (expressionBodiedProperties is not null)
        {
            CheckExpressionBodiedProperties(root, tree, expressionBodiedProperties, violations);
        }

        return violations.Count == 0
            ? "✅ Code style is correct."
            : $"❌ Found {violations.Count} style violation(s):\n" +
              string.Join('\n', violations.Select((v, i) => $"  {i + 1}. {v}"));
    }

    private static bool IsDisabled(IReadOnlyDictionary<string, string>? data)
        => data?.ContainsKey("__disabled") == true;

    /// <summary>
    /// Returns the brace preference, or <see langword="null"/> when the tool
    /// is disabled and no EditorConfig value is present.
    /// </summary>
    private static string? GetBracePreference(IReadOnlyDictionary<string, string>? data, bool disabled)
    {
        var value = data?.GetValueOrDefault("csharp_prefer_braces");
        return value ?? (disabled ? null : "true");
    }

    /// <summary>
    /// Returns the sort-system-directives-first preference, or <see langword="null"/>
    /// when the tool is disabled and no EditorConfig value is present.
    /// </summary>
    private static bool? GetSortSystemDirectivesFirst(IReadOnlyDictionary<string, string>? data, bool disabled)
    {
        var value = data?.GetValueOrDefault("dotnet_sort_system_directives_first");

        if (value is not null)
        {
            return !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
        }

        return disabled ? null : true;
    }

    private static string? GetExpressionBodiedPreference(IReadOnlyDictionary<string, string>? data, string key)
        => data?.GetValueOrDefault(key);

    // [csharp INST0005]: no #region directives
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

    // [csharp INST0022]: no decorative section-header comments
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

    // [csharp INST0018]: curly braces for control flow statements
    private static void CheckCurlyBraces(
        SyntaxNode root,
        SyntaxTree tree,
        string preference,
        List<string> violations)
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

            if (embedded is null)
            {
                continue;
            }

            var line = tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
            var keyword = GetControlFlowKeyword(node);

            switch (preference)
            {
                case "false":
                    if (embedded is BlockSyntax block && block.Statements.Count == 1
                        && !IsMultilineStatement(block.Statements[0], tree))
                    {
                        violations.Add(
                            $"Line {line}: '{keyword}' statement has unnecessary curly braces " +
                            "around a single-line body (csharp_prefer_braces = false).");
                    }

                    break;

                case "when_multiline":
                    if (embedded is BlockSyntax wmBlock && wmBlock.Statements.Count == 1
                        && !IsMultilineStatement(wmBlock.Statements[0], tree))
                    {
                        violations.Add(
                            $"Line {line}: '{keyword}' statement has unnecessary curly braces " +
                            "around a single-line body (csharp_prefer_braces = when_multiline).");
                    }
                    else if (embedded is not BlockSyntax
                             && IsMultilineStatement(embedded, tree))
                    {
                        violations.Add(
                            $"Line {line}: '{keyword}' statement requires curly braces " +
                            "around a multi-line body (csharp_prefer_braces = when_multiline).");
                    }

                    break;

                default: // "true" or unrecognized — require braces
                    if (embedded is not BlockSyntax && !IsGuardClause(node, embedded))
                    {
                        violations.Add(
                            $"Line {line}: '{keyword}' statement requires curly braces " +
                            "(exception: single-line guard clauses).");
                    }

                    break;
            }
        }
    }

    private static bool IsMultilineStatement(StatementSyntax statement, SyntaxTree tree)
    {
        var span = tree.GetLineSpan(statement.Span);

        return span.StartLinePosition.Line != span.EndLinePosition.Line;
    }

    // [csharp INST0015]: blank line before control flow statements
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

    // [csharp INST0017]: expression-body arrow on the next line
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

    // [csharp INST0021]: XML doc comments on public/protected members
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

    // EditorConfig: dotnet_sort_system_directives_first
    private static void CheckSortSystemDirectivesFirst(
        SyntaxNode root,
        SyntaxTree tree,
        bool sortSystemFirst,
        List<string> violations)
    {
        if (!sortSystemFirst)
        {
            return;
        }

        if (root is CompilationUnitSyntax compilationUnit)
        {
            CheckUsingsOrder(compilationUnit.Usings, tree, violations);
        }

        foreach (var namespaceDecl in root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
        {
            CheckUsingsOrder(namespaceDecl.Usings, tree, violations);
        }
    }

    private static void CheckUsingsOrder(
        SyntaxList<UsingDirectiveSyntax> usings,
        SyntaxTree tree,
        List<string> violations)
    {
        var seenNonSystem = false;

        foreach (var usingDirective in usings)
        {
            if (usingDirective.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)
                || usingDirective.StaticKeyword.IsKind(SyntaxKind.StaticKeyword)
                || usingDirective.Alias is not null)
            {
                continue;
            }

            var name = usingDirective.Name?.ToString() ?? string.Empty;
            var isSystem = name == "System" || name.StartsWith("System.", StringComparison.Ordinal);

            if (!isSystem)
            {
                seenNonSystem = true;
            }
            else if (seenNonSystem)
            {
                var line = tree.GetLineSpan(usingDirective.Span).StartLinePosition.Line + 1;
                violations.Add(
                    $"Line {line}: 'using {name}' must come before non-System using directives " +
                    "(dotnet_sort_system_directives_first = true).");
            }
        }
    }

    // EditorConfig: csharp_style_expression_bodied_methods
    private static void CheckExpressionBodiedMethods(
        SyntaxNode root,
        SyntaxTree tree,
        string? preference,
        List<string> violations)
    {
        if (preference is null)
        {
            return;
        }

        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            switch (preference)
            {
                case "false":
                case "never":
                    if (method.ExpressionBody is not null)
                    {
                        var line = tree.GetLineSpan(method.Span).StartLinePosition.Line + 1;
                        violations.Add(
                            $"Line {line}: Method '{method.Identifier.Text}' uses an expression body; " +
                            "prefer a block body (csharp_style_expression_bodied_methods = never).");
                    }

                    break;

                case "true":
                case "always":
                    if (method.Body is { } body
                        && method.ExpressionBody is null
                        && body.Statements.Count == 1
                        && body.Statements[0] is ReturnStatementSyntax { Expression: not null })
                    {
                        var line = tree.GetLineSpan(method.Span).StartLinePosition.Line + 1;
                        violations.Add(
                            $"Line {line}: Method '{method.Identifier.Text}' has a single return statement; " +
                            "prefer expression-body syntax (csharp_style_expression_bodied_methods = always).");
                    }

                    break;

                case "when_on_single_line":
                    if (method.Body is { } whenBody
                        && method.ExpressionBody is null
                        && whenBody.Statements.Count == 1
                        && whenBody.Statements[0] is ReturnStatementSyntax { Expression: not null } retStmt
                        && IsExpressionOnSingleLine(retStmt.Expression, tree))
                    {
                        var line = tree.GetLineSpan(method.Span).StartLinePosition.Line + 1;
                        violations.Add(
                            $"Line {line}: Method '{method.Identifier.Text}' has a single-line return; " +
                            "prefer expression-body syntax (csharp_style_expression_bodied_methods = when_on_single_line).");
                    }

                    break;

                default:
                    break;
            }
        }
    }

    // EditorConfig: csharp_style_expression_bodied_properties
    private static void CheckExpressionBodiedProperties(
        SyntaxNode root,
        SyntaxTree tree,
        string? preference,
        List<string> violations)
    {
        if (preference is null)
        {
            return;
        }

        foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            switch (preference)
            {
                case "false":
                case "never":
                    if (property.ExpressionBody is not null)
                    {
                        var line = tree.GetLineSpan(property.Span).StartLinePosition.Line + 1;
                        violations.Add(
                            $"Line {line}: Property '{property.Identifier.Text}' uses an expression body; " +
                            "prefer a block body with a getter accessor (csharp_style_expression_bodied_properties = never).");
                    }

                    break;

                case "true":
                case "always":
                    if (IsGetOnlySingleReturnProperty(property, out _))
                    {
                        var line = tree.GetLineSpan(property.Span).StartLinePosition.Line + 1;
                        violations.Add(
                            $"Line {line}: Property '{property.Identifier.Text}' has a get accessor with a single return; " +
                            "prefer expression-body syntax (csharp_style_expression_bodied_properties = always).");
                    }

                    break;

                case "when_on_single_line":
                    if (IsGetOnlySingleReturnProperty(property, out var returnExpr)
                        && returnExpr is not null
                        && IsExpressionOnSingleLine(returnExpr, tree))
                    {
                        var line = tree.GetLineSpan(property.Span).StartLinePosition.Line + 1;
                        violations.Add(
                            $"Line {line}: Property '{property.Identifier.Text}' has a single-line get return; " +
                            "prefer expression-body syntax (csharp_style_expression_bodied_properties = when_on_single_line).");
                    }

                    break;

                default:
                    break;
            }
        }
    }

    private static bool IsGetOnlySingleReturnProperty(
        PropertyDeclarationSyntax property,
        out ExpressionSyntax? returnExpression)
    {
        returnExpression = null;

        if (property.ExpressionBody is not null)
        {
            return false;
        }

        var accessorList = property.AccessorList;

        if (accessorList is null)
        {
            return false;
        }

        var accessors = accessorList.Accessors;

        if (accessors.Count != 1 || !accessors[0].IsKind(SyntaxKind.GetAccessorDeclaration))
        {
            return false;
        }

        var getter = accessors[0];

        if (getter.Body is null || getter.ExpressionBody is not null)
        {
            return false;
        }

        if (getter.Body.Statements.Count != 1
            || getter.Body.Statements[0] is not ReturnStatementSyntax { Expression: not null } returnStmt)
        {
            return false;
        }

        returnExpression = returnStmt.Expression;

        return true;
    }

    private static bool IsExpressionOnSingleLine(SyntaxNode node, SyntaxTree tree)
    {
        var span = tree.GetLineSpan(node.Span);

        return span.StartLinePosition.Line == span.EndLinePosition.Line;
    }

    [GeneratedRegex(
        @"^\s*//\s*[─═━—–\-_]{2,}\s*\S+.*[─═━—–\-_]{2,}|^\s*//\s*[─═━—–\-_]{3,}",
        RegexOptions.CultureInvariant)]
    private static partial Regex DecorativeCommentRegex();
}
