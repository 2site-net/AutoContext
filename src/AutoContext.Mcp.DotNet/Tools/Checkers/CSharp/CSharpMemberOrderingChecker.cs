namespace AutoContext.Mcp.DotNet.Tools.Checkers.CSharp;

using System.ComponentModel;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using ModelContextProtocol.Server;

using AutoContext.Mcp.Shared.Checkers;

/// <summary>
/// Validates that type members are ordered by kind, access level, and static-before-instance.
/// </summary>
[McpServerToolType]
public sealed class CSharpMemberOrderingChecker : IChecker
{
    /// <inheritdoc />
    public string ToolName
        => "check_csharp_member_ordering";

    private enum MemberKind
    {
        Constant = 0,
        StaticField = 1,
        Field = 2,
        Constructor = 3,
        Delegate = 4,
        Event = 5,
        Enum = 6,
        Property = 7,
        Indexer = 8,
        Method = 9,
        Operator = 10,
        NestedType = 11,
    }

    private enum AccessLevel
    {
        Public = 0,
        Internal = 1,
        ProtectedInternal = 2,
        Protected = 3,
        PrivateProtected = 4,
        Private = 5,
    }

    /// <summary>
    /// Checks that members within each type are ordered by kind, then access level, then static before instance.
    /// </summary>
    [McpServerTool(Name = "check_csharp_member_ordering", ReadOnly = true, Idempotent = true)]
    [Description(
        "Checks that C# type members follow the required ordering: " +
        "constants → static fields → fields → constructors → delegates → events → " +
        "enums → properties → indexers → methods → operators → nested types, " +
        "then public → private, then static before instance, then alphabetically. " +
        "Test classes (with [Fact], [Theory], [Test], or [TestCase] methods) are skipped.")]
    public async Task<string> CheckAsync(
        [Description("The C# source code to check.")]
        string content,
        IReadOnlyDictionary<string, string>? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var tree = CSharpSyntaxTree.ParseText(content);
        var root = await tree.GetRootAsync().ConfigureAwait(false);
        var violations = new List<string>();

        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            CheckType(typeDecl, tree, violations);
        }

        return violations.Count == 0
            ? "✅ Member ordering is correct."
            : $"❌ Found {violations.Count} ordering violation(s):\n" +
              string.Join('\n', violations.Select((v, i) => $"  {i + 1}. {v}"));
    }

    // [csharp INST0004]: member ordering by kind, access level, static, alphabetical
    private static void CheckType(TypeDeclarationSyntax typeDecl, SyntaxTree tree, List<string> violations)
    {
        if (TestDetection.IsTestClass(typeDecl))
        {
            return;
        }

        var typeName = typeDecl.Identifier.Text;
        var members = typeDecl.Members;

        MemberKind? previousKind = null;
        AccessLevel? previousAccess = null;
        bool? previousIsStatic = null;
        string? previousName = null;

        foreach (var member in members)
        {
            var kind = GetMemberKind(member);
            if (kind is null)
            {
                continue;
            }

            var access = GetAccessLevel(member);
            var isStatic = IsStatic(member);
            var name = GetMemberName(member);
            var line = tree.GetLineSpan(member.Span).StartLinePosition.Line + 1;

            if (previousKind is not null)
            {
                if (kind < previousKind)
                {
                    violations.Add(
                        $"{typeName}: {KindLabel(kind.Value)} '{name}' (line {line}) " +
                        $"should appear before {KindLabel(previousKind.Value)} '{previousName}'.");
                }
                else if (kind == previousKind)
                {
                    if (access < previousAccess)
                    {
                        violations.Add(
                            $"{typeName}: {AccessLabel(access)} member '{name}' (line {line}) " +
                            $"should appear before {AccessLabel(previousAccess!.Value)} member '{previousName}'.");
                    }
                    else if (access == previousAccess)
                    {
                        if (isStatic && previousIsStatic == false)
                        {
                            violations.Add(
                                $"{typeName}: static member '{name}' (line {line}) " +
                                $"should appear before instance member '{previousName}'.");
                        }
                        else if (isStatic == previousIsStatic
                                 && string.Compare(name, previousName, StringComparison.Ordinal) < 0)
                        {
                            violations.Add(
                                $"{typeName}: member '{name}' (line {line}) " +
                                $"should appear before '{previousName}' (alphabetical order).");
                        }
                    }
                }
            }

            previousKind = kind;
            previousAccess = access;
            previousIsStatic = isStatic;
            previousName = name;
        }
    }

    private static MemberKind? GetMemberKind(MemberDeclarationSyntax member) =>
        member switch
        {
            FieldDeclarationSyntax f when f.Modifiers.Any(SyntaxKind.ConstKeyword) => MemberKind.Constant,
            FieldDeclarationSyntax when member.Modifiers.Any(SyntaxKind.StaticKeyword) => MemberKind.StaticField,
            FieldDeclarationSyntax => MemberKind.Field,
            ConstructorDeclarationSyntax => MemberKind.Constructor,
            DestructorDeclarationSyntax => MemberKind.Constructor,
            DelegateDeclarationSyntax => MemberKind.Delegate,
            EventDeclarationSyntax => MemberKind.Event,
            EventFieldDeclarationSyntax => MemberKind.Event,
            EnumDeclarationSyntax => MemberKind.Enum,
            PropertyDeclarationSyntax => MemberKind.Property,
            IndexerDeclarationSyntax => MemberKind.Indexer,
            MethodDeclarationSyntax => MemberKind.Method,
            OperatorDeclarationSyntax => MemberKind.Operator,
            ConversionOperatorDeclarationSyntax => MemberKind.Operator,
            TypeDeclarationSyntax => MemberKind.NestedType,
            _ => null,
        };

    private static AccessLevel GetAccessLevel(MemberDeclarationSyntax member)
    {
        var modifiers = member.Modifiers;

        var hasPublic = modifiers.Any(SyntaxKind.PublicKeyword);
        var hasInternal = modifiers.Any(SyntaxKind.InternalKeyword);
        var hasProtected = modifiers.Any(SyntaxKind.ProtectedKeyword);
        var hasPrivate = modifiers.Any(SyntaxKind.PrivateKeyword);

        return (hasPublic, hasInternal, hasProtected, hasPrivate) switch
        {
            (true, _, _, _) => AccessLevel.Public,
            (_, true, true, _) => AccessLevel.ProtectedInternal,
            (_, true, _, _) => AccessLevel.Internal,
            (_, _, true, true) => AccessLevel.PrivateProtected,
            (_, _, true, _) => AccessLevel.Protected,
            (_, _, _, true) => AccessLevel.Private,
            _ => AccessLevel.Private, // C# default
        };
    }

    private static bool IsStatic(MemberDeclarationSyntax member) =>
        member.Modifiers.Any(SyntaxKind.StaticKeyword);

    private static string GetMemberName(MemberDeclarationSyntax member) =>
        member switch
        {
            FieldDeclarationSyntax f => f.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "(field)",
            PropertyDeclarationSyntax p => p.Identifier.Text,
            MethodDeclarationSyntax m => m.Identifier.Text,
            ConstructorDeclarationSyntax c => c.Identifier.Text,
            DestructorDeclarationSyntax d => $"~{d.Identifier.Text}",
            TypeDeclarationSyntax t => t.Identifier.Text,
            EnumDeclarationSyntax e => e.Identifier.Text,
            DelegateDeclarationSyntax d => d.Identifier.Text,
            IndexerDeclarationSyntax => "this[]",
            EventDeclarationSyntax ev => ev.Identifier.Text,
            EventFieldDeclarationSyntax ef => ef.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "(event)",
            OperatorDeclarationSyntax op => $"operator {op.OperatorToken.Text}",
            ConversionOperatorDeclarationSyntax co => $"operator {co.Type}",
            _ => "(unknown)",
        };

    private static string KindLabel(MemberKind kind) =>
        kind switch
        {
            MemberKind.Constant => "constant",
            MemberKind.StaticField => "static field",
            MemberKind.Field => "field",
            MemberKind.Constructor => "constructor",
            MemberKind.Delegate => "delegate",
            MemberKind.Event => "event",
            MemberKind.Enum => "enum",
            MemberKind.Property => "property",
            MemberKind.Indexer => "indexer",
            MemberKind.Method => "method",
            MemberKind.Operator => "operator",
            MemberKind.NestedType => "nested type",
            _ => kind.ToString(),
        };

    private static string AccessLabel(AccessLevel access) =>
        access switch
        {
            AccessLevel.Public => "public",
            AccessLevel.Internal => "internal",
            AccessLevel.ProtectedInternal => "protected internal",
            AccessLevel.Protected => "protected",
            AccessLevel.PrivateProtected => "private protected",
            AccessLevel.Private => "private",
            _ => access.ToString(),
        };
}
