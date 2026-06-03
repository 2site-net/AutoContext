namespace AutoContext.Engine.Core.Workspace.Context;

using AutoContext.Engine.Protocol.Messages.Workspace;

/// <summary>
/// Projects the immutable <see cref="WorkspaceDetectionResult"/> domain
/// value onto the <see cref="JsonWorkspaceDetectResult"/> wire shape
/// returned by the <c>Workspace.Detect</c> RPC. The mapping is explicit
/// and exhaustive: every flag the wire contract declares is read from the
/// detection result's flag set, so the wire schema — not the rule tables —
/// stays the single authority on which flags ever cross the boundary. A
/// raised flag name in <see cref="WorkspaceDetectionResult.Flags"/> maps
/// to <see langword="true"/>; every other flag defaults to
/// <see langword="false"/>. The result carries no <c>overrides</c> field:
/// the override inventory is owned elsewhere and reachable via
/// <c>Instructions.List</c>.
/// </summary>
internal static class WorkspaceDetectionResultExtensions
{
    /// <summary>
    /// Projects a detection result onto its wire representation.
    /// </summary>
    /// <param name="result">The domain detection result. Must not be
    /// <see langword="null"/>.</param>
    /// <returns>The equivalent
    /// <see cref="JsonWorkspaceDetectResult"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/>
    /// is <see langword="null"/>.</exception>
    public static JsonWorkspaceDetectResult ToWireFormat(this WorkspaceDetectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new JsonWorkspaceDetectResult
        {
            Extensions = [.. result.Extensions],
            Flags = new JsonWorkspaceFlags
            {
                HasAngular = result.Has("hasAngular"),
                HasAspNetCore = result.Has("hasAspNetCore"),
                HasBash = result.Has("hasBash"),
                HasBatch = result.Has("hasBatch"),
                HasBlazor = result.Has("hasBlazor"),
                HasC = result.Has("hasC"),
                HasCpp = result.Has("hasCpp"),
                HasCSharp = result.Has("hasCSharp"),
                HasCss = result.Has("hasCss"),
                HasCypress = result.Has("hasCypress"),
                HasDapper = result.Has("hasDapper"),
                HasDart = result.Has("hasDart"),
                HasDocker = result.Has("hasDocker"),
                HasDotNet = result.Has("hasDotNet"),
                HasDotNetTesting = result.Has("hasDotNetTesting"),
                HasEntityFrameworkCore = result.Has("hasEntityFrameworkCore"),
                HasFSharp = result.Has("hasFSharp"),
                HasGit = result.Has("hasGit"),
                HasGo = result.Has("hasGo"),
                HasGraphql = result.Has("hasGraphql"),
                HasGroovy = result.Has("hasGroovy"),
                HasGrpc = result.Has("hasGrpc"),
                HasHtml = result.Has("hasHtml"),
                HasJasmine = result.Has("hasJasmine"),
                HasJava = result.Has("hasJava"),
                HasJavaScript = result.Has("hasJavaScript"),
                HasJest = result.Has("hasJest"),
                HasJvm = result.Has("hasJvm"),
                HasKotlin = result.Has("hasKotlin"),
                HasLua = result.Has("hasLua"),
                HasMaui = result.Has("hasMaui"),
                HasMediatR = result.Has("hasMediatR"),
                HasMocha = result.Has("hasMocha"),
                HasMongoDb = result.Has("hasMongoDb"),
                HasMsTest = result.Has("hasMsTest"),
                HasMySql = result.Has("hasMySql"),
                HasNative = result.Has("hasNative"),
                HasNextJs = result.Has("hasNextJs"),
                HasNodeJs = result.Has("hasNodeJs"),
                HasNUnit = result.Has("hasNUnit"),
                HasOracle = result.Has("hasOracle"),
                HasPhp = result.Has("hasPhp"),
                HasPlaywright = result.Has("hasPlaywright"),
                HasPostgres = result.Has("hasPostgres"),
                HasPowerShell = result.Has("hasPowerShell"),
                HasPython = result.Has("hasPython"),
                HasRazor = result.Has("hasRazor"),
                HasReact = result.Has("hasReact"),
                HasRedis = result.Has("hasRedis"),
                HasRuby = result.Has("hasRuby"),
                HasRust = result.Has("hasRust"),
                HasScala = result.Has("hasScala"),
                HasSignalR = result.Has("hasSignalR"),
                HasSqlite = result.Has("hasSqlite"),
                HasSqlServer = result.Has("hasSqlServer"),
                HasSvelte = result.Has("hasSvelte"),
                HasSwift = result.Has("hasSwift"),
                HasTypeScript = result.Has("hasTypeScript"),
                HasUnity = result.Has("hasUnity"),
                HasVbNet = result.Has("hasVbNet"),
                HasVitest = result.Has("hasVitest"),
                HasVue = result.Has("hasVue"),
                HasWebForms = result.Has("hasWebForms"),
                HasWebTesting = result.Has("hasWebTesting"),
                HasWinForms = result.Has("hasWinForms"),
                HasWpf = result.Has("hasWpf"),
                HasXaml = result.Has("hasXaml"),
                HasXunit = result.Has("hasXunit"),
                HasYaml = result.Has("hasYaml"),
            },
        };
    }
}
