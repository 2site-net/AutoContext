namespace AutoContext.Engine.Protocol.Messages.Workspace;

using System.Text.Json.Serialization;

/// <summary>
/// The full set of boolean technology flags raised by workspace
/// detection, returned inside <see cref="JsonWorkspaceDetectResult"/>.
/// Each flag answers "does this workspace contain X?" — a language,
/// framework, test runner, database, or tool. Flags are raised
/// directly by file-extension and content scans and then propagated
/// up the activation cascade (e.g. <see cref="HasVitest"/> also
/// raises <see cref="HasNodeJs"/> and <see cref="HasWebTesting"/>),
/// so a consumer sees both the leaf signal and every parent it
/// implies. Every flag defaults to <see langword="false"/>; the
/// engine sets only those it positively detected. See
/// <c>design § Workspace detection</c>.
/// </summary>
public sealed record JsonWorkspaceFlags
{
    /// <summary>Angular (npm dependency).</summary>
    [JsonPropertyName("hasAngular")]
    public bool HasAngular { get; init; }

    /// <summary>ASP.NET Core (.NET package reference).</summary>
    [JsonPropertyName("hasAspNetCore")]
    public bool HasAspNetCore { get; init; }

    /// <summary>Bash / shell scripts.</summary>
    [JsonPropertyName("hasBash")]
    public bool HasBash { get; init; }

    /// <summary>Windows batch scripts.</summary>
    [JsonPropertyName("hasBatch")]
    public bool HasBatch { get; init; }

    /// <summary>Blazor components.</summary>
    [JsonPropertyName("hasBlazor")]
    public bool HasBlazor { get; init; }

    /// <summary>C source.</summary>
    [JsonPropertyName("hasC")]
    public bool HasC { get; init; }

    /// <summary>C++ source.</summary>
    [JsonPropertyName("hasCpp")]
    public bool HasCpp { get; init; }

    /// <summary>C# source.</summary>
    [JsonPropertyName("hasCSharp")]
    public bool HasCSharp { get; init; }

    /// <summary>CSS stylesheets.</summary>
    [JsonPropertyName("hasCss")]
    public bool HasCss { get; init; }

    /// <summary>Cypress end-to-end tests (npm dependency).</summary>
    [JsonPropertyName("hasCypress")]
    public bool HasCypress { get; init; }

    /// <summary>Dapper (.NET package reference).</summary>
    [JsonPropertyName("hasDapper")]
    public bool HasDapper { get; init; }

    /// <summary>Dart source.</summary>
    [JsonPropertyName("hasDart")]
    public bool HasDart { get; init; }

    /// <summary>Docker artifacts (Dockerfile / compose).</summary>
    [JsonPropertyName("hasDocker")]
    public bool HasDocker { get; init; }

    /// <summary>.NET projects (any <c>*.csproj</c>/<c>*.fsproj</c>/<c>*.vbproj</c>).</summary>
    [JsonPropertyName("hasDotNet")]
    public bool HasDotNet { get; init; }

    /// <summary>A .NET test framework — parent raised by xUnit / MSTest / NUnit.</summary>
    [JsonPropertyName("hasDotNetTesting")]
    public bool HasDotNetTesting { get; init; }

    /// <summary>Entity Framework Core (.NET package reference).</summary>
    [JsonPropertyName("hasEntityFrameworkCore")]
    public bool HasEntityFrameworkCore { get; init; }

    /// <summary>F# source.</summary>
    [JsonPropertyName("hasFSharp")]
    public bool HasFSharp { get; init; }

    /// <summary>A Git repository (<c>.git</c> directory).</summary>
    [JsonPropertyName("hasGit")]
    public bool HasGit { get; init; }

    /// <summary>Go source.</summary>
    [JsonPropertyName("hasGo")]
    public bool HasGo { get; init; }

    /// <summary>GraphQL (npm or .NET dependency).</summary>
    [JsonPropertyName("hasGraphql")]
    public bool HasGraphql { get; init; }

    /// <summary>Groovy source.</summary>
    [JsonPropertyName("hasGroovy")]
    public bool HasGroovy { get; init; }

    /// <summary>gRPC (.NET package reference).</summary>
    [JsonPropertyName("hasGrpc")]
    public bool HasGrpc { get; init; }

    /// <summary>HTML markup.</summary>
    [JsonPropertyName("hasHtml")]
    public bool HasHtml { get; init; }

    /// <summary>Jasmine tests (npm dependency).</summary>
    [JsonPropertyName("hasJasmine")]
    public bool HasJasmine { get; init; }

    /// <summary>Java source.</summary>
    [JsonPropertyName("hasJava")]
    public bool HasJava { get; init; }

    /// <summary>JavaScript source.</summary>
    [JsonPropertyName("hasJavaScript")]
    public bool HasJavaScript { get; init; }

    /// <summary>Jest tests (npm dependency).</summary>
    [JsonPropertyName("hasJest")]
    public bool HasJest { get; init; }

    /// <summary>A JVM language — parent raised by Java / Kotlin / Scala / Groovy.</summary>
    [JsonPropertyName("hasJvm")]
    public bool HasJvm { get; init; }

    /// <summary>Kotlin source.</summary>
    [JsonPropertyName("hasKotlin")]
    public bool HasKotlin { get; init; }

    /// <summary>Lua source.</summary>
    [JsonPropertyName("hasLua")]
    public bool HasLua { get; init; }

    /// <summary>.NET MAUI (.NET package reference).</summary>
    [JsonPropertyName("hasMaui")]
    public bool HasMaui { get; init; }

    /// <summary>MediatR (.NET package reference).</summary>
    [JsonPropertyName("hasMediatR")]
    public bool HasMediatR { get; init; }

    /// <summary>Mocha tests (npm dependency).</summary>
    [JsonPropertyName("hasMocha")]
    public bool HasMocha { get; init; }

    /// <summary>MongoDB (.NET package reference).</summary>
    [JsonPropertyName("hasMongoDb")]
    public bool HasMongoDb { get; init; }

    /// <summary>MSTest (.NET package reference).</summary>
    [JsonPropertyName("hasMsTest")]
    public bool HasMsTest { get; init; }

    /// <summary>MySQL (.NET package reference).</summary>
    [JsonPropertyName("hasMySql")]
    public bool HasMySql { get; init; }

    /// <summary>A native-toolchain language — parent raised by C / C++ / Rust / Swift / Go.</summary>
    [JsonPropertyName("hasNative")]
    public bool HasNative { get; init; }

    /// <summary>Next.js (npm dependency).</summary>
    [JsonPropertyName("hasNextJs")]
    public bool HasNextJs { get; init; }

    /// <summary>A Node.js project — parent raised by npm-ecosystem signals.</summary>
    [JsonPropertyName("hasNodeJs")]
    public bool HasNodeJs { get; init; }

    /// <summary>NUnit (.NET package reference).</summary>
    [JsonPropertyName("hasNUnit")]
    public bool HasNUnit { get; init; }

    /// <summary>Oracle database (.NET package reference).</summary>
    [JsonPropertyName("hasOracle")]
    public bool HasOracle { get; init; }

    /// <summary>PHP source.</summary>
    [JsonPropertyName("hasPhp")]
    public bool HasPhp { get; init; }

    /// <summary>Playwright tests (npm dependency).</summary>
    [JsonPropertyName("hasPlaywright")]
    public bool HasPlaywright { get; init; }

    /// <summary>PostgreSQL (.NET package reference).</summary>
    [JsonPropertyName("hasPostgres")]
    public bool HasPostgres { get; init; }

    /// <summary>PowerShell scripts.</summary>
    [JsonPropertyName("hasPowerShell")]
    public bool HasPowerShell { get; init; }

    /// <summary>Python source.</summary>
    [JsonPropertyName("hasPython")]
    public bool HasPython { get; init; }

    /// <summary>Razor markup.</summary>
    [JsonPropertyName("hasRazor")]
    public bool HasRazor { get; init; }

    /// <summary>React (npm dependency).</summary>
    [JsonPropertyName("hasReact")]
    public bool HasReact { get; init; }

    /// <summary>Redis (.NET package reference).</summary>
    [JsonPropertyName("hasRedis")]
    public bool HasRedis { get; init; }

    /// <summary>Ruby source.</summary>
    [JsonPropertyName("hasRuby")]
    public bool HasRuby { get; init; }

    /// <summary>Rust source.</summary>
    [JsonPropertyName("hasRust")]
    public bool HasRust { get; init; }

    /// <summary>Scala source.</summary>
    [JsonPropertyName("hasScala")]
    public bool HasScala { get; init; }

    /// <summary>SignalR (.NET package reference).</summary>
    [JsonPropertyName("hasSignalR")]
    public bool HasSignalR { get; init; }

    /// <summary>SQLite (.NET package reference).</summary>
    [JsonPropertyName("hasSqlite")]
    public bool HasSqlite { get; init; }

    /// <summary>SQL Server (.NET package reference).</summary>
    [JsonPropertyName("hasSqlServer")]
    public bool HasSqlServer { get; init; }

    /// <summary>Svelte (npm dependency).</summary>
    [JsonPropertyName("hasSvelte")]
    public bool HasSvelte { get; init; }

    /// <summary>Swift source.</summary>
    [JsonPropertyName("hasSwift")]
    public bool HasSwift { get; init; }

    /// <summary>TypeScript source.</summary>
    [JsonPropertyName("hasTypeScript")]
    public bool HasTypeScript { get; init; }

    /// <summary>Unity project assets.</summary>
    [JsonPropertyName("hasUnity")]
    public bool HasUnity { get; init; }

    /// <summary>VB.NET source.</summary>
    [JsonPropertyName("hasVbNet")]
    public bool HasVbNet { get; init; }

    /// <summary>Vitest tests (npm dependency).</summary>
    [JsonPropertyName("hasVitest")]
    public bool HasVitest { get; init; }

    /// <summary>Vue (npm dependency).</summary>
    [JsonPropertyName("hasVue")]
    public bool HasVue { get; init; }

    /// <summary>ASP.NET Web Forms markup.</summary>
    [JsonPropertyName("hasWebForms")]
    public bool HasWebForms { get; init; }

    /// <summary>A web test framework — parent raised by Vitest / Jest / Jasmine / Mocha / Playwright / Cypress.</summary>
    [JsonPropertyName("hasWebTesting")]
    public bool HasWebTesting { get; init; }

    /// <summary>Windows Forms (.NET package reference).</summary>
    [JsonPropertyName("hasWinForms")]
    public bool HasWinForms { get; init; }

    /// <summary>WPF (.NET package reference).</summary>
    [JsonPropertyName("hasWpf")]
    public bool HasWpf { get; init; }

    /// <summary>XAML markup.</summary>
    [JsonPropertyName("hasXaml")]
    public bool HasXaml { get; init; }

    /// <summary>xUnit (.NET package reference).</summary>
    [JsonPropertyName("hasXunit")]
    public bool HasXunit { get; init; }

    /// <summary>YAML files.</summary>
    [JsonPropertyName("hasYaml")]
    public bool HasYaml { get; init; }
}
