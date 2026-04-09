import type { McpToolsEntry } from './types/mcp-tools-entry.js';
import type { McpServerEntry } from './types/mcp-server-entry.js';
import type { InstructionsFileEntry } from './types/instructions-file-entry.js';

// ── Extension identifiers ────────────────────────────────────────────

export const commandIds = {
    AutoConfigure: 'sharppilot.autoConfigure',
    ShowNotDetected: 'sharppilot.showNotDetected',
    HideNotDetected: 'sharppilot.hideNotDetected',
    ToggleInstruction: 'sharppilot.toggleInstruction',
    ResetInstructions: 'sharppilot.resetInstructions',
    EnableInstruction: 'sharppilot.enableInstruction',
    DisableInstruction: 'sharppilot.disableInstruction',
    DeleteOverride: 'sharppilot.deleteOverride',
    ShowOriginal: 'sharppilot.showOriginal',
    EnterExportMode: 'sharppilot.enterExportMode',
    ConfirmExport: 'sharppilot.confirmExport',
    CancelExport: 'sharppilot.cancelExport',
} as const;

export const viewIds = {
    Instructions: 'sharppilot.instructionsView',
    Tools: 'sharppilot.toolsView',
} as const;

export const contextKeys = {
    ExportMode: 'sharppilot.exportMode',
} as const;

// ── Instructions ─────────────────────────────────────────────────────

// Category order: General → Languages → Platforms (.NET, Web) → Tools.
// Within each category, entries are sorted alphabetically by label.
// package.json (chatInstructions + configuration.properties) should follow the same order.
//
// Each entry's `key` is expanded to a full `settingId` (`sharppilot.instructions.<key>`)
// by `InstructionsCatalogEntry` at construction time — see `InstructionsCatalog`.
export const instructionsFiles: readonly InstructionsFileEntry[] = [
    { key: 'codeReview', fileName: 'code-review.instructions.md', label: 'Code Review', category: 'General' },
    { key: 'designPrinciples', fileName: 'design-principles.instructions.md', label: 'Design Principles', category: 'General' },
    { key: 'restApiDesign', fileName: 'rest-api-design.instructions.md', label: 'REST API Design', category: 'General' },
    { key: 'testing', fileName: 'testing.instructions.md', label: 'Testing', category: 'General', contextKeys: ['hasDotNetTesting', 'hasWebTesting'] },
    { key: 'lang.bash', fileName: 'lang-bash.instructions.md', label: 'Bash', category: 'Languages', contextKeys: ['hasBash'] },
    { key: 'lang.batch', fileName: 'lang-batch.instructions.md', label: 'Batch (CMD)', category: 'Languages', contextKeys: ['hasBatch'] },
    { key: 'lang.c', fileName: 'lang-c.instructions.md', label: 'C', category: 'Languages', contextKeys: ['hasC'] },
    { key: 'lang.csharp', fileName: 'lang-csharp.instructions.md', label: 'C#', category: 'Languages', contextKeys: ['hasCSharp'] },
    { key: 'lang.cpp', fileName: 'lang-cpp.instructions.md', label: 'C++', category: 'Languages', contextKeys: ['hasCpp'] },
    { key: 'lang.css', fileName: 'lang-css.instructions.md', label: 'CSS', category: 'Languages', contextKeys: ['hasCss'] },
    { key: 'lang.dart', fileName: 'lang-dart.instructions.md', label: 'Dart', category: 'Languages', contextKeys: ['hasDart'] },
    { key: 'lang.fsharp', fileName: 'lang-fsharp.instructions.md', label: 'F#', category: 'Languages', contextKeys: ['hasFSharp'] },
    { key: 'lang.go', fileName: 'lang-go.instructions.md', label: 'Go', category: 'Languages', contextKeys: ['hasGo'] },
    { key: 'lang.graphql', fileName: 'lang-graphql.instructions.md', label: 'GraphQL', category: 'Languages', contextKeys: ['hasGraphql'] },
    { key: 'lang.groovy', fileName: 'lang-groovy.instructions.md', label: 'Groovy', category: 'Languages', contextKeys: ['hasGroovy'] },
    { key: 'lang.html', fileName: 'lang-html.instructions.md', label: 'HTML', category: 'Languages', contextKeys: ['hasHtml'] },
    { key: 'lang.java', fileName: 'lang-java.instructions.md', label: 'Java', category: 'Languages', contextKeys: ['hasJava'] },
    { key: 'lang.javascript', fileName: 'lang-javascript.instructions.md', label: 'JavaScript', category: 'Languages', contextKeys: ['hasJavaScript', 'hasTypeScript'] },
    { key: 'lang.kotlin', fileName: 'lang-kotlin.instructions.md', label: 'Kotlin', category: 'Languages', contextKeys: ['hasKotlin'] },
    { key: 'lang.lua', fileName: 'lang-lua.instructions.md', label: 'Lua', category: 'Languages', contextKeys: ['hasLua'] },
    { key: 'lang.php', fileName: 'lang-php.instructions.md', label: 'PHP', category: 'Languages', contextKeys: ['hasPhp'] },
    { key: 'lang.powershell', fileName: 'lang-powershell.instructions.md', label: 'PowerShell', category: 'Languages', contextKeys: ['hasPowerShell'] },
    { key: 'lang.python', fileName: 'lang-python.instructions.md', label: 'Python', category: 'Languages', contextKeys: ['hasPython'] },
    { key: 'lang.ruby', fileName: 'lang-ruby.instructions.md', label: 'Ruby', category: 'Languages', contextKeys: ['hasRuby'] },
    { key: 'lang.rust', fileName: 'lang-rust.instructions.md', label: 'Rust', category: 'Languages', contextKeys: ['hasRust'] },
    { key: 'lang.scala', fileName: 'lang-scala.instructions.md', label: 'Scala', category: 'Languages', contextKeys: ['hasScala'] },
    { key: 'lang.sql', fileName: 'lang-sql.instructions.md', label: 'SQL', category: 'Languages' },
    { key: 'lang.swift', fileName: 'lang-swift.instructions.md', label: 'Swift', category: 'Languages', contextKeys: ['hasSwift'] },
    { key: 'lang.typescript', fileName: 'lang-typescript.instructions.md', label: 'TypeScript', category: 'Languages', contextKeys: ['hasTypeScript'] },
    { key: 'lang.vbnet', fileName: 'lang-vbnet.instructions.md', label: 'VB.NET', category: 'Languages', contextKeys: ['hasVbNet'] },
    { key: 'lang.yaml', fileName: 'lang-yaml.instructions.md', label: 'YAML', category: 'Languages', contextKeys: ['hasYaml'] },
    { key: 'dotnet.maui', fileName: 'dotnet-maui.instructions.md', label: '.NET MAUI', category: '.NET', contextKeys: ['hasMaui'] },
    { key: 'dotnet.aspnetCore', fileName: 'dotnet-aspnetcore.instructions.md', label: 'ASP.NET Core', category: '.NET', contextKeys: ['hasAspNetCore'] },
    { key: 'dotnet.aspx', fileName: 'dotnet-aspx.instructions.md', label: 'ASP.NET Web Forms', category: '.NET', contextKeys: ['hasWebForms'] },
    { key: 'dotnet.asyncAwait', fileName: 'dotnet-async-await.instructions.md', label: 'Async/Await', category: '.NET', contextKeys: ['hasDotNet'] },
    { key: 'dotnet.blazor', fileName: 'dotnet-blazor.instructions.md', label: 'Blazor', category: '.NET', contextKeys: ['hasBlazor'] },
    { key: 'dotnet.codingStandards', fileName: 'dotnet-coding-standards.instructions.md', label: 'Coding Standards', category: '.NET', contextKeys: ['hasDotNet'] },
    { key: 'dotnet.core', fileName: 'dotnet-core.instructions.md', label: 'Core (DI, Logging, Config, Security)', category: '.NET', contextKeys: ['hasDotNet'] },
    { key: 'dotnet.dapper', fileName: 'dotnet-dapper.instructions.md', label: 'Dapper', category: '.NET', contextKeys: ['hasDapper'] },
    { key: 'dotnet.debugging', fileName: 'dotnet-debugging.instructions.md', label: 'Debugging', category: '.NET', contextKeys: ['hasDotNet'] },
    { key: 'dotnet.entityFrameworkCore', fileName: 'dotnet-entity-framework-core.instructions.md', label: 'Entity Framework Core', category: '.NET', contextKeys: ['hasEntityFrameworkCore'] },
    { key: 'dotnet.grpc', fileName: 'dotnet-grpc.instructions.md', label: 'gRPC', category: '.NET', contextKeys: ['hasGrpc'] },
    { key: 'dotnet.mediatorCqrs', fileName: 'dotnet-mediator-cqrs.instructions.md', label: 'Mediator / CQRS', category: '.NET', contextKeys: ['hasMediatR'] },
    { key: 'dotnet.mongodb', fileName: 'dotnet-mongodb.instructions.md', label: 'MongoDB', category: '.NET', contextKeys: ['hasMongoDb'] },
    { key: 'dotnet.mstest', fileName: 'dotnet-mstest.instructions.md', label: 'MSTest', category: '.NET', contextKeys: ['hasMsTest'] },
    { key: 'dotnet.mysql', fileName: 'dotnet-mysql.instructions.md', label: 'MySQL', category: '.NET', contextKeys: ['hasMySql'] },
    { key: 'dotnet.nuget', fileName: 'dotnet-nuget.instructions.md', label: 'NuGet', category: '.NET', contextKeys: ['hasDotNet'] },
    { key: 'dotnet.nunit', fileName: 'dotnet-nunit.instructions.md', label: 'NUnit', category: '.NET', contextKeys: ['hasNUnit'] },
    { key: 'dotnet.oracle', fileName: 'dotnet-oracle.instructions.md', label: 'Oracle', category: '.NET', contextKeys: ['hasOracle'] },
    { key: 'dotnet.performanceMemory', fileName: 'dotnet-performance-memory.instructions.md', label: 'Performance & Memory', category: '.NET', contextKeys: ['hasDotNet'] },
    { key: 'dotnet.postgresql', fileName: 'dotnet-postgresql.instructions.md', label: 'PostgreSQL', category: '.NET', contextKeys: ['hasPostgres'] },
    { key: 'dotnet.razor', fileName: 'dotnet-razor.instructions.md', label: 'Razor', category: '.NET', contextKeys: ['hasRazor'] },
    { key: 'dotnet.redis', fileName: 'dotnet-redis.instructions.md', label: 'Redis', category: '.NET', contextKeys: ['hasRedis'] },
    { key: 'dotnet.signalR', fileName: 'dotnet-signalr.instructions.md', label: 'SignalR', category: '.NET', contextKeys: ['hasSignalR'] },
    { key: 'dotnet.sqlServer', fileName: 'dotnet-sql-server.instructions.md', label: 'SQL Server', category: '.NET', contextKeys: ['hasSqlServer'] },
    { key: 'dotnet.sqlite', fileName: 'dotnet-sqlite.instructions.md', label: 'SQLite', category: '.NET', contextKeys: ['hasSqlite'] },
    { key: 'dotnet.testing', fileName: 'dotnet-testing.instructions.md', label: 'Testing', category: '.NET', contextKeys: ['hasDotNetTesting'] },
    { key: 'dotnet.unity', fileName: 'dotnet-unity.instructions.md', label: 'Unity', category: '.NET', contextKeys: ['hasUnity'] },
    { key: 'dotnet.winForms', fileName: 'dotnet-winforms.instructions.md', label: 'Windows Forms', category: '.NET', contextKeys: ['hasWinForms'] },
    { key: 'dotnet.wpf', fileName: 'dotnet-wpf.instructions.md', label: 'WPF', category: '.NET', contextKeys: ['hasWpf'] },
    { key: 'dotnet.xaml', fileName: 'dotnet-xaml.instructions.md', label: 'XAML', category: '.NET', contextKeys: ['hasXaml'] },
    { key: 'dotnet.xunit', fileName: 'dotnet-xunit.instructions.md', label: 'xUnit', category: '.NET', contextKeys: ['hasXunit'] },
    { key: 'web.angular', fileName: 'web-angular.instructions.md', label: 'Angular', category: 'Web', contextKeys: ['hasAngular'] },
    { key: 'web.cypress', fileName: 'web-cypress.instructions.md', label: 'Cypress', category: 'Web', contextKeys: ['hasCypress'] },
    { key: 'web.jasmine', fileName: 'web-jasmine.instructions.md', label: 'Jasmine', category: 'Web', contextKeys: ['hasJasmine'] },
    { key: 'web.jest', fileName: 'web-jest.instructions.md', label: 'Jest', category: 'Web', contextKeys: ['hasJest'] },
    { key: 'web.mocha', fileName: 'web-mocha.instructions.md', label: 'Mocha', category: 'Web', contextKeys: ['hasMocha'] },
    { key: 'web.nextJs', fileName: 'web-nextjs.instructions.md', label: 'Next.js', category: 'Web', contextKeys: ['hasNextJs'] },
    { key: 'web.nodeJs', fileName: 'web-nodejs.instructions.md', label: 'Node.js', category: 'Web', contextKeys: ['hasNodeJs'] },
    { key: 'web.playwright', fileName: 'web-playwright.instructions.md', label: 'Playwright', category: 'Web', contextKeys: ['hasPlaywright'] },
    { key: 'web.react', fileName: 'web-react.instructions.md', label: 'React', category: 'Web', contextKeys: ['hasReact'] },
    { key: 'web.svelte', fileName: 'web-svelte.instructions.md', label: 'Svelte', category: 'Web', contextKeys: ['hasSvelte'] },
    { key: 'web.testing', fileName: 'web-testing.instructions.md', label: 'Testing', category: 'Web', contextKeys: ['hasWebTesting'] },
    { key: 'web.vitest', fileName: 'web-vitest.instructions.md', label: 'Vitest', category: 'Web', contextKeys: ['hasVitest'] },
    { key: 'web.vue', fileName: 'web-vue.instructions.md', label: 'Vue.js', category: 'Web', contextKeys: ['hasVue'] },
    { key: 'git.commitFormat', fileName: 'git-commit-format.instructions.md', label: 'Commit Format', category: 'Tools', contextKeys: ['hasGit'] },
    { key: 'docker', fileName: 'docker.instructions.md', label: 'Docker', category: 'Tools', contextKeys: ['hasDocker'] },
];

export const instructionsCategoryOrder: readonly string[] = ['General', 'Languages', '.NET', 'Web', 'Tools'];

// ── MCP Tools ────────────────────────────────────────────────────────

// Each entry's `key` is expanded to a full `settingId` (`sharppilot.mcpTools.<key>`) by
// `McpToolsCatalogEntry` at construction time — see `McpToolsCatalog`.
// When `toolName` is present the entry is a sub-feature of a composite tool (e.g.
// `check_csharp_all`); when absent, `key` doubles as the MCP tool name.
export const mcpTools: readonly McpToolsEntry[] = [
    { key: 'check_csharp_async_patterns', toolName: 'check_csharp_all', label: 'Async Patterns', category: 'C#', group: '.NET', serverCategory: 'dotnet', contextKeys: ['hasCSharp'] },
    { key: 'check_csharp_coding_style', toolName: 'check_csharp_all', label: 'Coding Style', category: 'C#', group: '.NET', serverCategory: 'dotnet', contextKeys: ['hasCSharp'] },
    { key: 'check_csharp_member_ordering', toolName: 'check_csharp_all', label: 'Member Ordering', category: 'C#', group: '.NET', serverCategory: 'dotnet', contextKeys: ['hasCSharp'] },
    { key: 'check_csharp_naming_conventions', toolName: 'check_csharp_all', label: 'Naming Conventions', category: 'C#', group: '.NET', serverCategory: 'dotnet', contextKeys: ['hasCSharp'] },
    { key: 'check_csharp_nullable_context', toolName: 'check_csharp_all', label: 'Nullable Context', category: 'C#', group: '.NET', serverCategory: 'dotnet', contextKeys: ['hasCSharp'] },
    { key: 'check_csharp_project_structure', toolName: 'check_csharp_all', label: 'Project Structure', category: 'C#', group: '.NET', serverCategory: 'dotnet', contextKeys: ['hasCSharp'] },
    { key: 'check_csharp_test_style', toolName: 'check_csharp_all', label: 'Test Style', category: 'C#', group: '.NET', serverCategory: 'dotnet', contextKeys: ['hasCSharp'] },
    { key: 'check_nuget_hygiene', label: 'NuGet Hygiene', category: 'NuGet', group: '.NET', serverCategory: 'dotnet', contextKeys: ['hasDotNet'] },
    { key: 'check_git_commit_content', toolName: 'check_git_all', label: 'Commit Content', category: 'Git', group: 'Workspace', serverCategory: 'git', contextKeys: ['hasGit'] },
    { key: 'check_git_commit_format', toolName: 'check_git_all', label: 'Commit Format', category: 'Git', group: 'Workspace', serverCategory: 'git', contextKeys: ['hasGit'] },
    { key: 'get_editorconfig', label: 'EditorConfig', category: 'EditorConfig', group: 'Workspace', serverCategory: 'editorconfig' },
    { key: 'check_typescript_coding_style', toolName: 'check_typescript_all', label: 'Coding Style', category: 'TypeScript', group: 'Web', serverCategory: 'typescript', contextKeys: ['hasTypeScript'] },
];

export const mcpToolGroupOrder: readonly string[] = ['.NET', 'Web', 'Workspace'];
export const mcpToolCategoryOrder: readonly string[] = ['C#', 'NuGet', 'TypeScript', 'Git', 'EditorConfig'];

// ── MCP Servers ──────────────────────────────────────────────────────

export const mcpServers: readonly McpServerEntry[] = [
    { label: 'SharpPilot: DotNet', category: 'dotnet', process: 'dotnet', contextKey: 'hasDotNet' },
    { label: 'SharpPilot: Git', category: 'git', process: 'workspace', contextKey: 'hasGit' },
    { label: 'SharpPilot: EditorConfig', category: 'editorconfig', process: 'workspace' },
    { label: 'SharpPilot: TypeScript', category: 'typescript', process: 'web', contextKey: 'hasTypeScript' },
];

// ── Tree View Labels ─────────────────────────────────────────────────

export const treeViewLabels = {
    activeSuffix: 'active',
    activeTooltip: 'Active — included in Copilot context',
    disabled: 'disabled',
    disabledTooltip: 'Disabled — turned off in settings',
    enabledTooltip: 'Enabled — available to Copilot',
    featuresEnabledTooltip: 'features enabled',
    notDetected: 'not detected',
    notDetectedTooltip: 'Not detected — workspace lacks matching files',
    overridden: 'overridden',
    overriddenTooltip: 'Overridden — local .github/instructions file found',
    settingPrefix: 'Setting:',
} as const;
