import type { InstructionsFileEntry } from './types/instructions-file-entry.js';

// ── Extension identifiers ────────────────────────────────────────────

export const commandIds = {
    AutoConfigure: 'autocontext.auto-configure',
    ShowNotDetected: 'autocontext.show-not-detected',
    HideNotDetected: 'autocontext.hide-not-detected',
    ToggleInstruction: 'autocontext.toggle-instruction',
    ResetInstructions: 'autocontext.reset-instructions',
    EnableInstruction: 'autocontext.enable-instruction',
    DisableInstruction: 'autocontext.disable-instruction',
    DeleteOverride: 'autocontext.delete-override',
    ShowOriginal: 'autocontext.show-original',
    ShowChangelog: 'autocontext.show-changelog',
    ShowWhatsNew: 'autocontext.show-whats-new',
    EnterExportMode: 'autocontext.enter-export-mode',
    ConfirmExport: 'autocontext.confirm-export',
    CancelExport: 'autocontext.cancel-export',
    StartMcpServer: 'autocontext.start-mcp-server',
    StopMcpServer: 'autocontext.stop-mcp-server',
    RestartMcpServer: 'autocontext.restart-mcp-server',
    ShowMcpServerOutput: 'autocontext.show-mcp-server-output',
} as const;

export const viewIds = {
    Instructions: 'autocontext.instructions-view',
    Tools: 'autocontext.mcp-tools-view',
} as const;

export const contextKeys = {
    ExportMode: 'autocontext.export-mode',
    HasWhatsNew: 'autocontext.has-whats-new',
} as const;

export const globalStateKeys = {
    LastSeenVersion: 'autocontext.lastSeenVersion',
} as const;

// ── Instructions ─────────────────────────────────────────────────────

// Category order: General → Languages → Platforms (.NET, Web) → Tools.
// Within each category, entries are sorted alphabetically by label.
// package.json (chatInstructions) should follow the same order.
//
// Each entry's `key` is expanded to a full `contextKey` (`autocontext.instructions.<key>`)
// by `InstructionsCatalogEntry` at construction time — see `InstructionsCatalog`.
export const instructionsFiles: readonly InstructionsFileEntry[] = [
    { key: 'codeReview', fileName: 'code-review.instructions.md', label: 'Code Review', category: 'General' },
    { key: 'designPrinciples', fileName: 'design-principles.instructions.md', label: 'Design Principles', category: 'General' },
    { key: 'restApiDesign', fileName: 'rest-api-design.instructions.md', label: 'REST API Design', category: 'General' },
    { key: 'testing', fileName: 'testing.instructions.md', label: 'Testing', category: 'General', workspaceFlags: ['hasDotNetTesting', 'hasWebTesting'] },
    { key: 'lang.bash', fileName: 'lang-bash.instructions.md', label: 'Bash', category: 'Languages', workspaceFlags: ['hasBash'] },
    { key: 'lang.batch', fileName: 'lang-batch.instructions.md', label: 'Batch (CMD)', category: 'Languages', workspaceFlags: ['hasBatch'] },
    { key: 'lang.c', fileName: 'lang-c.instructions.md', label: 'C', category: 'Languages', workspaceFlags: ['hasC'] },
    { key: 'lang.csharp', fileName: 'lang-csharp.instructions.md', label: 'C#', category: 'Languages', workspaceFlags: ['hasCSharp'] },
    { key: 'lang.cpp', fileName: 'lang-cpp.instructions.md', label: 'C++', category: 'Languages', workspaceFlags: ['hasCpp'] },
    { key: 'lang.css', fileName: 'lang-css.instructions.md', label: 'CSS', category: 'Languages', workspaceFlags: ['hasCss'] },
    { key: 'lang.dart', fileName: 'lang-dart.instructions.md', label: 'Dart', category: 'Languages', workspaceFlags: ['hasDart'] },
    { key: 'lang.fsharp', fileName: 'lang-fsharp.instructions.md', label: 'F#', category: 'Languages', workspaceFlags: ['hasFSharp'] },
    { key: 'lang.go', fileName: 'lang-go.instructions.md', label: 'Go', category: 'Languages', workspaceFlags: ['hasGo'] },
    { key: 'lang.graphql', fileName: 'lang-graphql.instructions.md', label: 'GraphQL', category: 'Languages', workspaceFlags: ['hasGraphql'] },
    { key: 'lang.groovy', fileName: 'lang-groovy.instructions.md', label: 'Groovy', category: 'Languages', workspaceFlags: ['hasGroovy'] },
    { key: 'lang.html', fileName: 'lang-html.instructions.md', label: 'HTML', category: 'Languages', workspaceFlags: ['hasHtml'] },
    { key: 'lang.java', fileName: 'lang-java.instructions.md', label: 'Java', category: 'Languages', workspaceFlags: ['hasJava'] },
    { key: 'lang.javascript', fileName: 'lang-javascript.instructions.md', label: 'JavaScript', category: 'Languages', workspaceFlags: ['hasJavaScript', 'hasTypeScript'] },
    { key: 'lang.kotlin', fileName: 'lang-kotlin.instructions.md', label: 'Kotlin', category: 'Languages', workspaceFlags: ['hasKotlin'] },
    { key: 'lang.lua', fileName: 'lang-lua.instructions.md', label: 'Lua', category: 'Languages', workspaceFlags: ['hasLua'] },
    { key: 'lang.php', fileName: 'lang-php.instructions.md', label: 'PHP', category: 'Languages', workspaceFlags: ['hasPhp'] },
    { key: 'lang.powershell', fileName: 'lang-powershell.instructions.md', label: 'PowerShell', category: 'Languages', workspaceFlags: ['hasPowerShell'] },
    { key: 'lang.python', fileName: 'lang-python.instructions.md', label: 'Python', category: 'Languages', workspaceFlags: ['hasPython'] },
    { key: 'lang.ruby', fileName: 'lang-ruby.instructions.md', label: 'Ruby', category: 'Languages', workspaceFlags: ['hasRuby'] },
    { key: 'lang.rust', fileName: 'lang-rust.instructions.md', label: 'Rust', category: 'Languages', workspaceFlags: ['hasRust'] },
    { key: 'lang.scala', fileName: 'lang-scala.instructions.md', label: 'Scala', category: 'Languages', workspaceFlags: ['hasScala'] },
    { key: 'lang.sql', fileName: 'lang-sql.instructions.md', label: 'SQL', category: 'Languages' },
    { key: 'lang.swift', fileName: 'lang-swift.instructions.md', label: 'Swift', category: 'Languages', workspaceFlags: ['hasSwift'] },
    { key: 'lang.typescript', fileName: 'lang-typescript.instructions.md', label: 'TypeScript', category: 'Languages', workspaceFlags: ['hasTypeScript'] },
    { key: 'lang.vbnet', fileName: 'lang-vbnet.instructions.md', label: 'VB.NET', category: 'Languages', workspaceFlags: ['hasVbNet'] },
    { key: 'lang.yaml', fileName: 'lang-yaml.instructions.md', label: 'YAML', category: 'Languages', workspaceFlags: ['hasYaml'] },
    { key: 'dotnet.maui', fileName: 'dotnet-maui.instructions.md', label: '.NET MAUI', category: '.NET', workspaceFlags: ['hasMaui'] },
    { key: 'dotnet.aspnetCore', fileName: 'dotnet-aspnetcore.instructions.md', label: 'ASP.NET Core', category: '.NET', workspaceFlags: ['hasAspNetCore'] },
    { key: 'dotnet.aspx', fileName: 'dotnet-aspx.instructions.md', label: 'ASP.NET Web Forms', category: '.NET', workspaceFlags: ['hasWebForms'] },
    { key: 'dotnet.asyncAwait', fileName: 'dotnet-async-await.instructions.md', label: 'Async/Await', category: '.NET', workspaceFlags: ['hasDotNet'] },
    { key: 'dotnet.blazor', fileName: 'dotnet-blazor.instructions.md', label: 'Blazor', category: '.NET', workspaceFlags: ['hasBlazor'] },
    { key: 'dotnet.codingStandards', fileName: 'dotnet-coding-standards.instructions.md', label: 'Coding Standards', category: '.NET', workspaceFlags: ['hasDotNet'] },
    { key: 'dotnet.core', fileName: 'dotnet-core.instructions.md', label: 'Core (DI, Logging, Config, Security)', category: '.NET', workspaceFlags: ['hasDotNet'] },
    { key: 'dotnet.dapper', fileName: 'dotnet-dapper.instructions.md', label: 'Dapper', category: '.NET', workspaceFlags: ['hasDapper'] },
    { key: 'dotnet.debugging', fileName: 'dotnet-debugging.instructions.md', label: 'Debugging', category: '.NET', workspaceFlags: ['hasDotNet'] },
    { key: 'dotnet.entityFrameworkCore', fileName: 'dotnet-entity-framework-core.instructions.md', label: 'Entity Framework Core', category: '.NET', workspaceFlags: ['hasEntityFrameworkCore'] },
    { key: 'dotnet.grpc', fileName: 'dotnet-grpc.instructions.md', label: 'gRPC', category: '.NET', workspaceFlags: ['hasGrpc'] },
    { key: 'dotnet.mediatorCqrs', fileName: 'dotnet-mediator-cqrs.instructions.md', label: 'Mediator / CQRS', category: '.NET', workspaceFlags: ['hasMediatR'] },
    { key: 'dotnet.mongodb', fileName: 'dotnet-mongodb.instructions.md', label: 'MongoDB', category: '.NET', workspaceFlags: ['hasMongoDb'] },
    { key: 'dotnet.mstest', fileName: 'dotnet-mstest.instructions.md', label: 'MSTest', category: '.NET', workspaceFlags: ['hasMsTest'] },
    { key: 'dotnet.mysql', fileName: 'dotnet-mysql.instructions.md', label: 'MySQL', category: '.NET', workspaceFlags: ['hasMySql'] },
    { key: 'dotnet.nuget', fileName: 'dotnet-nuget.instructions.md', label: 'NuGet', category: '.NET', workspaceFlags: ['hasDotNet'] },
    { key: 'dotnet.nunit', fileName: 'dotnet-nunit.instructions.md', label: 'NUnit', category: '.NET', workspaceFlags: ['hasNUnit'] },
    { key: 'dotnet.oracle', fileName: 'dotnet-oracle.instructions.md', label: 'Oracle', category: '.NET', workspaceFlags: ['hasOracle'] },
    { key: 'dotnet.performanceMemory', fileName: 'dotnet-performance-memory.instructions.md', label: 'Performance & Memory', category: '.NET', workspaceFlags: ['hasDotNet'] },
    { key: 'dotnet.postgresql', fileName: 'dotnet-postgresql.instructions.md', label: 'PostgreSQL', category: '.NET', workspaceFlags: ['hasPostgres'] },
    { key: 'dotnet.razor', fileName: 'dotnet-razor.instructions.md', label: 'Razor', category: '.NET', workspaceFlags: ['hasRazor'] },
    { key: 'dotnet.redis', fileName: 'dotnet-redis.instructions.md', label: 'Redis', category: '.NET', workspaceFlags: ['hasRedis'] },
    { key: 'dotnet.signalR', fileName: 'dotnet-signalr.instructions.md', label: 'SignalR', category: '.NET', workspaceFlags: ['hasSignalR'] },
    { key: 'dotnet.sqlServer', fileName: 'dotnet-sql-server.instructions.md', label: 'SQL Server', category: '.NET', workspaceFlags: ['hasSqlServer'] },
    { key: 'dotnet.sqlite', fileName: 'dotnet-sqlite.instructions.md', label: 'SQLite', category: '.NET', workspaceFlags: ['hasSqlite'] },
    { key: 'dotnet.testing', fileName: 'dotnet-testing.instructions.md', label: 'Testing', category: '.NET', workspaceFlags: ['hasDotNetTesting'] },
    { key: 'dotnet.unity', fileName: 'dotnet-unity.instructions.md', label: 'Unity', category: '.NET', workspaceFlags: ['hasUnity'] },
    { key: 'dotnet.winForms', fileName: 'dotnet-winforms.instructions.md', label: 'Windows Forms', category: '.NET', workspaceFlags: ['hasWinForms'] },
    { key: 'dotnet.wpf', fileName: 'dotnet-wpf.instructions.md', label: 'WPF', category: '.NET', workspaceFlags: ['hasWpf'] },
    { key: 'dotnet.xaml', fileName: 'dotnet-xaml.instructions.md', label: 'XAML', category: '.NET', workspaceFlags: ['hasXaml'] },
    { key: 'dotnet.xunit', fileName: 'dotnet-xunit.instructions.md', label: 'xUnit', category: '.NET', workspaceFlags: ['hasXunit'] },
    { key: 'web.angular', fileName: 'web-angular.instructions.md', label: 'Angular', category: 'Web', workspaceFlags: ['hasAngular'] },
    { key: 'web.cypress', fileName: 'web-cypress.instructions.md', label: 'Cypress', category: 'Web', workspaceFlags: ['hasCypress'] },
    { key: 'web.jasmine', fileName: 'web-jasmine.instructions.md', label: 'Jasmine', category: 'Web', workspaceFlags: ['hasJasmine'] },
    { key: 'web.jest', fileName: 'web-jest.instructions.md', label: 'Jest', category: 'Web', workspaceFlags: ['hasJest'] },
    { key: 'web.mocha', fileName: 'web-mocha.instructions.md', label: 'Mocha', category: 'Web', workspaceFlags: ['hasMocha'] },
    { key: 'web.nextJs', fileName: 'web-nextjs.instructions.md', label: 'Next.js', category: 'Web', workspaceFlags: ['hasNextJs'] },
    { key: 'web.nodeJs', fileName: 'web-nodejs.instructions.md', label: 'Node.js', category: 'Web', workspaceFlags: ['hasNodeJs'] },
    { key: 'web.playwright', fileName: 'web-playwright.instructions.md', label: 'Playwright', category: 'Web', workspaceFlags: ['hasPlaywright'] },
    { key: 'web.react', fileName: 'web-react.instructions.md', label: 'React', category: 'Web', workspaceFlags: ['hasReact'] },
    { key: 'web.svelte', fileName: 'web-svelte.instructions.md', label: 'Svelte', category: 'Web', workspaceFlags: ['hasSvelte'] },
    { key: 'web.testing', fileName: 'web-testing.instructions.md', label: 'Testing', category: 'Web', workspaceFlags: ['hasWebTesting'] },
    { key: 'web.vitest', fileName: 'web-vitest.instructions.md', label: 'Vitest', category: 'Web', workspaceFlags: ['hasVitest'] },
    { key: 'web.vue', fileName: 'web-vue.instructions.md', label: 'Vue.js', category: 'Web', workspaceFlags: ['hasVue'] },
    { key: 'git.commitFormat', fileName: 'git-commit-format.instructions.md', label: 'Commit Format', category: 'Tools', workspaceFlags: ['hasGit'] },
    { key: 'docker', fileName: 'docker.instructions.md', label: 'Docker', category: 'Tools', workspaceFlags: ['hasDocker'] },
];

export const instructionsCategoryOrder: readonly string[] = ['General', 'Languages', '.NET', 'Web', 'Tools'];

// ── Tree View Labels ─────────────────────────────────────────────────

export const treeViewLabels = {
    activeSuffix: 'active',
    activeTooltip: 'Active — included in Copilot context',
    disabled: 'disabled',
    disabledTooltip: 'Disabled — turned off in settings',
    enabledTooltip: 'Enabled — available to Copilot',
    tasksEnabledTooltip: 'tasks enabled',
    notDetected: 'not detected',
    notDetectedTooltip: 'Not detected — workspace lacks matching files',
    outdated: 'overridden (outdated)',
    outdatedTooltip: 'Overridden — the local file is outdated, a newer version is available',
    overridden: 'overridden',
    overriddenTooltip: 'Overridden — using a local file instead of AutoContext\'s version',
    contextKeyPrefix: 'Context Key:',
} as const;
