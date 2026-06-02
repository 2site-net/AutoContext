namespace AutoContext.Engine.Core.Workspace.Context;

using System.Text.RegularExpressions;

using static AutoContext.Engine.Core.Workspace.Context.FileSelectorKind;

/// <summary>
/// The declarative source of truth for workspace context detection: the
/// three rule tables derived from the VS Code extension's
/// <c>workspace-context-detector.ts</c>. Same flag names, same regex
/// patterns, and same <c>[child, parent]</c> activation edges as the
/// TypeScript port — the existing flag set is the contract, and these
/// tables carry no behaviour. File rules and content scans both select
/// files through typed <see cref="FileSelectorKind"/> selectors rather
/// than glob strings so the detector classifies files by lookup instead
/// of re-parsing globs, and content detection is grouped by manifest into
/// <see cref="ContentScan"/> entries so a new platform is a data edit
/// rather than a new type. The detector resolves each table from DI as
/// the corresponding <see cref="IReadOnlyList{T}"/> singleton.
/// </summary>
internal static partial class WorkspaceDetectionRules
{
    /// <summary>
    /// Content-scan groups: each pairs a set of manifest selectors with
    /// the pattern rules tested against those files' bodies. The npm
    /// group scans <c>package.json</c> (patterns case-sensitive except
    /// <c>hasGraphql</c>); the .NET group scans <c>.csproj</c>/
    /// <c>.fsproj</c>/<c>.vbproj</c> (all patterns case-insensitive).
    /// </summary>
    public static readonly IReadOnlyList<ContentScan> ContentScans =
    [
        new(
            [new("package.json", FileName)],
            [
                new("hasReact", ReactNpmPattern()),
                new("hasAngular", AngularNpmPattern()),
                new("hasVue", VueNpmPattern()),
                new("hasSvelte", SvelteNpmPattern()),
                new("hasVitest", VitestNpmPattern()),
                new("hasJest", JestNpmPattern()),
                new("hasJasmine", JasmineNpmPattern()),
                new("hasMocha", MochaNpmPattern()),
                new("hasPlaywright", PlaywrightNpmPattern()),
                new("hasCypress", CypressNpmPattern()),
                new("hasNextJs", NextNpmPattern()),
                new("hasGraphql", GraphqlNpmPattern()),
            ]),
        new(
            [new("csproj", Extension), new("fsproj", Extension), new("vbproj", Extension)],
            [
                new("hasAspNetCore", AspNetCoreProjectPattern()),
                new("hasDapper", DapperProjectPattern()),
                new("hasEntityFrameworkCore", EntityFrameworkCoreProjectPattern()),
                new("hasMaui", MauiProjectPattern()),
                new("hasMongoDb", MongoDbProjectPattern()),
                new("hasXunit", XunitProjectPattern()),
                new("hasMsTest", MsTestProjectPattern()),
                new("hasNUnit", NUnitProjectPattern()),
                new("hasWpf", WpfProjectPattern()),
                new("hasWinForms", WinFormsProjectPattern()),
                new("hasMySql", MySqlProjectPattern()),
                new("hasOracle", OracleProjectPattern()),
                new("hasPostgres", PostgresProjectPattern()),
                new("hasSqlite", SqliteProjectPattern()),
                new("hasSqlServer", SqlServerProjectPattern()),
                new("hasGrpc", GrpcProjectPattern()),
                new("hasMediatR", MediatRProjectPattern()),
                new("hasRedis", RedisProjectPattern()),
                new("hasSignalR", SignalRProjectPattern()),
                new("hasGraphql", GraphqlProjectPattern()),
            ]),
    ];

    /// <summary>
    /// File-presence rules: each flag is set when any workspace file
    /// matches one of the rule's selectors. Each selector is a single
    /// criterion — a file extension, an exact file name, or (for the few
    /// cases that need it) a glob pattern.
    /// </summary>
    public static readonly IReadOnlyList<FilePresenceRule> FileRules =
    [
        new("hasDotNet", [new("csproj", Extension), new("fsproj", Extension), new("vbproj", Extension), new("sln", Extension), new("slnx", Extension)]),
        new("hasCSharp", [new("csproj", Extension)]),
        new("hasFSharp", [new("fsproj", Extension)]),
        new("hasVbNet", [new("vbproj", Extension)]),
        new("hasBlazor", [new("razor", Extension)]),
        new("hasXaml", [new("xaml", Extension)]),
        new("hasWebForms", [new("aspx", Extension), new("ascx", Extension), new("master", Extension)]),
        new("hasRazor", [new("cshtml", Extension)]),
        new("hasHtml", [new("html", Extension), new("cshtml", Extension)]),
        new("hasCss", [new("css", Extension)]),
        new("hasDart", [new("dart", Extension), new("pubspec.yaml", FileName)]),
        new("hasJavaScript", [new("js", Extension), new("jsx", Extension), new("mjs", Extension), new("cjs", Extension)]),
        new("hasTypeScript", [new("ts", Extension), new("tsx", Extension), new("mts", Extension), new("cts", Extension)]),
        new("hasUnity", [new("**/ProjectSettings/ProjectSettings.asset", GlobPattern)]),
        new("hasDocker", [new("**/Dockerfile*", GlobPattern)]),
        new("hasPowerShell", [new("ps1", Extension), new("psm1", Extension), new("psd1", Extension)]),
        new("hasBash", [new("sh", Extension), new("bash", Extension)]),
        new("hasBatch", [new("bat", Extension), new("cmd", Extension)]),
        new("hasYaml", [new("yml", Extension), new("yaml", Extension)]),
        new("hasJava", [new("java", Extension), new("pom.xml", FileName), new("build.gradle", FileName)]),
        new("hasKotlin", [new("kt", Extension), new("kts", Extension)]),
        new("hasScala", [new("scala", Extension), new("sc", Extension), new("build.sbt", FileName)]),
        new("hasGroovy", [new("groovy", Extension), new("gvy", Extension)]),
        new("hasC", [new("c", Extension)]),
        new("hasCpp", [new("cpp", Extension), new("cxx", Extension), new("cc", Extension)]),
        new("hasRuby", [new("rb", Extension), new("Gemfile", FileName)]),
        new("hasRust", [new("rs", Extension), new("Cargo.toml", FileName)]),
        new("hasSwift", [new("swift", Extension), new("Package.swift", FileName)]),
        new("hasGo", [new("go", Extension), new("go.mod", FileName)]),
        new("hasPython", [new("py", Extension), new("pyproject.toml", FileName)]),
        new("hasLua", [new("lua", Extension)]),
        new("hasPhp", [new("php", Extension), new("composer.json", FileName)]),
    ];

    /// <summary>
    /// Activation edges walked after base detection: when the child flag
    /// is set, the parent flag is implied and set too.
    /// </summary>
    public static readonly IReadOnlyList<FlagActivationEdge> FlagActivationEdges =
    [
        new("hasNextJs", "hasReact"),
        new("hasAngular", "hasTypeScript"),
        new("hasTypeScript", "hasJavaScript"),
        new("hasReact", "hasNodeJs"),
        new("hasAngular", "hasNodeJs"),
        new("hasVue", "hasNodeJs"),
        new("hasSvelte", "hasNodeJs"),
        new("hasVitest", "hasNodeJs"),
        new("hasJest", "hasNodeJs"),
        new("hasJasmine", "hasNodeJs"),
        new("hasMocha", "hasNodeJs"),
        new("hasPlaywright", "hasNodeJs"),
        new("hasCypress", "hasNodeJs"),
        new("hasNodeJs", "hasJavaScript"),
        new("hasBlazor", "hasAspNetCore"),
        new("hasSignalR", "hasAspNetCore"),
        new("hasAspNetCore", "hasRazor"),
        new("hasBlazor", "hasCSharp"),
        new("hasUnity", "hasCSharp"),
        new("hasWpf", "hasXaml"),
        new("hasMaui", "hasXaml"),
        new("hasAspNetCore", "hasDotNet"),
        new("hasDapper", "hasDotNet"),
        new("hasEntityFrameworkCore", "hasDotNet"),
        new("hasMaui", "hasDotNet"),
        new("hasWpf", "hasDotNet"),
        new("hasWinForms", "hasDotNet"),
        new("hasWebForms", "hasDotNet"),
        new("hasGrpc", "hasDotNet"),
        new("hasMediatR", "hasDotNet"),
        new("hasRedis", "hasDotNet"),
        new("hasSignalR", "hasDotNet"),
        new("hasXunit", "hasDotNet"),
        new("hasMsTest", "hasDotNet"),
        new("hasNUnit", "hasDotNet"),
        new("hasMongoDb", "hasDotNet"),
        new("hasMySql", "hasDotNet"),
        new("hasOracle", "hasDotNet"),
        new("hasPostgres", "hasDotNet"),
        new("hasSqlite", "hasDotNet"),
        new("hasSqlServer", "hasDotNet"),
        new("hasUnity", "hasDotNet"),
        new("hasBlazor", "hasHtml"),
        new("hasHtml", "hasCss"),
        new("hasJava", "hasJvm"),
        new("hasKotlin", "hasJvm"),
        new("hasScala", "hasJvm"),
        new("hasGroovy", "hasJvm"),
        new("hasC", "hasNative"),
        new("hasCpp", "hasNative"),
        new("hasRust", "hasNative"),
        new("hasGo", "hasNative"),
        new("hasXunit", "hasDotNetTesting"),
        new("hasMsTest", "hasDotNetTesting"),
        new("hasNUnit", "hasDotNetTesting"),
        new("hasVitest", "hasWebTesting"),
        new("hasJest", "hasWebTesting"),
        new("hasJasmine", "hasWebTesting"),
        new("hasMocha", "hasWebTesting"),
        new("hasPlaywright", "hasWebTesting"),
        new("hasCypress", "hasWebTesting"),
    ];

    [GeneratedRegex(@"""@angular/core""\s*:")]
    private static partial Regex AngularNpmPattern();

    [GeneratedRegex(@"Sdk\s*=\s*[""']Microsoft\.NET\.Sdk\.(Web|Razor|BlazorWebAssembly)[""']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AspNetCoreProjectPattern();

    [GeneratedRegex(@"""cypress""\s*:")]
    private static partial Regex CypressNpmPattern();

    [GeneratedRegex(@"Dapper", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DapperProjectPattern();

    [GeneratedRegex(@"Microsoft\.EntityFrameworkCore", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EntityFrameworkCoreProjectPattern();

    [GeneratedRegex(@"""graphql""\s*:|""@apollo/|""graphql-request""\s*:|""urql""\s*:|""HotChocolate", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GraphqlNpmPattern();

    [GeneratedRegex(@"HotChocolate|GraphQL\.Server", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GraphqlProjectPattern();

    [GeneratedRegex(@"Grpc\.|Google\.Protobuf", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GrpcProjectPattern();

    [GeneratedRegex(@"""jasmine""\s*:")]
    private static partial Regex JasmineNpmPattern();

    [GeneratedRegex(@"""jest""\s*:")]
    private static partial Regex JestNpmPattern();

    [GeneratedRegex(@"<UseMaui>\s*true\s*</UseMaui>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MauiProjectPattern();

    [GeneratedRegex(@"MediatR|Mediator\.Abstractions", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MediatRProjectPattern();

    [GeneratedRegex(@"""mocha""\s*:")]
    private static partial Regex MochaNpmPattern();

    [GeneratedRegex(@"MongoDB\.Driver|MongoDB\.EntityFrameworkCore", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MongoDbProjectPattern();

    [GeneratedRegex(@"MSTest|Microsoft\.VisualStudio\.TestPlatform", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MsTestProjectPattern();

    [GeneratedRegex(@"MySqlConnector|MySql\.Data|Pomelo\.EntityFrameworkCore\.MySql", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MySqlProjectPattern();

    [GeneratedRegex(@"NUnit", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NUnitProjectPattern();

    [GeneratedRegex(@"""next""\s*:")]
    private static partial Regex NextNpmPattern();

    [GeneratedRegex(@"Oracle\.ManagedDataAccess|Oracle\.EntityFrameworkCore", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OracleProjectPattern();

    [GeneratedRegex(@"""@playwright/test""\s*:")]
    private static partial Regex PlaywrightNpmPattern();

    [GeneratedRegex(@"Npgsql", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PostgresProjectPattern();

    [GeneratedRegex(@"""react""\s*:")]
    private static partial Regex ReactNpmPattern();

    [GeneratedRegex(@"StackExchange\.Redis|Microsoft\.Extensions\.Caching\.StackExchangeRedis", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RedisProjectPattern();

    [GeneratedRegex(@"Microsoft\.AspNetCore\.SignalR", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SignalRProjectPattern();

    [GeneratedRegex(@"Microsoft\.Data\.SqlClient|System\.Data\.SqlClient|EntityFrameworkCore\.SqlServer|EntityFramework\.SqlServer", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SqlServerProjectPattern();

    [GeneratedRegex(@"Microsoft\.Data\.Sqlite|System\.Data\.SQLite|EntityFrameworkCore\.Sqlite", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SqliteProjectPattern();

    [GeneratedRegex(@"""svelte""\s*:")]
    private static partial Regex SvelteNpmPattern();

    [GeneratedRegex(@"""vitest""\s*:")]
    private static partial Regex VitestNpmPattern();

    [GeneratedRegex(@"""vue""\s*:")]
    private static partial Regex VueNpmPattern();

    [GeneratedRegex(@"<UseWindowsForms>\s*true\s*</UseWindowsForms>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WinFormsProjectPattern();

    [GeneratedRegex(@"<UseWPF>\s*true\s*</UseWPF>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WpfProjectPattern();

    [GeneratedRegex(@"xunit", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex XunitProjectPattern();
}
