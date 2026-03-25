export interface ToggleEntry {
    settingId: string;
    label: string;
    category: string;
}

export interface ServerEntry {
    label: string;
    scope: string;
    contextKey?: string;
}

export interface InstructionEntry extends ToggleEntry {
    fileName: string;
}

export interface ToolEntry extends ToggleEntry {
    toolName: string;
}

export const servers: readonly ServerEntry[] = [
    { label: 'SharpPilot: DotNet', scope: 'dotnet', contextKey: 'hasDotnet' },
    { label: 'SharpPilot: Git', scope: 'git', contextKey: 'hasGit' },
    { label: 'SharpPilot: EditorConfig', scope: 'editorconfig' },
];

export const instructions: readonly InstructionEntry[] = [
    { settingId: 'sharppilot.instructions.codeReview', fileName: 'code-review.instructions.md', label: 'Code Review', category: 'General' },
    { settingId: 'sharppilot.instructions.designPrinciples', fileName: 'design-principles.instructions.md', label: 'Design Principles', category: 'General' },
    { settingId: 'sharppilot.instructions.docker', fileName: 'docker.instructions.md', label: 'Docker', category: 'General' },
    { settingId: 'sharppilot.instructions.graphql', fileName: 'graphql.instructions.md', label: 'GraphQL', category: 'General' },
    { settingId: 'sharppilot.instructions.restApiDesign', fileName: 'rest-api-design.instructions.md', label: 'REST API Design', category: 'General' },
    { settingId: 'sharppilot.instructions.sql', fileName: 'sql.instructions.md', label: 'SQL', category: 'General' },
    { settingId: 'sharppilot.instructions.dotnet.aspnetCore', fileName: 'dotnet-aspnetcore.instructions.md', label: 'ASP.NET Core', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.asyncAwait', fileName: 'dotnet-async-await.instructions.md', label: 'Async/Await', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.blazor', fileName: 'dotnet-blazor.instructions.md', label: 'Blazor', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.codingStandards', fileName: 'dotnet-coding-standards.instructions.md', label: 'Coding Standards', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.core', fileName: 'dotnet-core.instructions.md', label: 'Core (DI, Logging, Config, Security)', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.dapper', fileName: 'dotnet-dapper.instructions.md', label: 'Dapper', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.debugging', fileName: 'dotnet-debugging.instructions.md', label: 'Debugging', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.entityFrameworkCore', fileName: 'dotnet-entity-framework-core.instructions.md', label: 'Entity Framework Core', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.grpc', fileName: 'dotnet-grpc.instructions.md', label: 'gRPC', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.maui', fileName: 'dotnet-maui.instructions.md', label: '.NET MAUI', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.mediatorCqrs', fileName: 'dotnet-mediator-cqrs.instructions.md', label: 'Mediator / CQRS', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.mongodb', fileName: 'dotnet-mongodb.instructions.md', label: 'MongoDB', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.mysql', fileName: 'dotnet-mysql.instructions.md', label: 'MySQL', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.nuget', fileName: 'dotnet-nuget.instructions.md', label: 'NuGet', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.oracle', fileName: 'dotnet-oracle.instructions.md', label: 'Oracle', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.performanceMemory', fileName: 'dotnet-performance-memory.instructions.md', label: 'Performance & Memory', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.postgresql', fileName: 'dotnet-postgresql.instructions.md', label: 'PostgreSQL', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.razor', fileName: 'dotnet-razor.instructions.md', label: 'Razor', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.redis', fileName: 'dotnet-redis.instructions.md', label: 'Redis', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.signalR', fileName: 'dotnet-signalr.instructions.md', label: 'SignalR', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.sqlite', fileName: 'dotnet-sqlite.instructions.md', label: 'SQLite', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.sqlServer', fileName: 'dotnet-sql-server.instructions.md', label: 'SQL Server', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.testing', fileName: 'dotnet-testing.instructions.md', label: 'Testing', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.unity', fileName: 'dotnet-unity.instructions.md', label: 'Unity', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.winForms', fileName: 'dotnet-winforms.instructions.md', label: 'Windows Forms', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.wpf', fileName: 'dotnet-wpf.instructions.md', label: 'WPF', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.xunit', fileName: 'dotnet-xunit.instructions.md', label: 'xUnit', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.mstest', fileName: 'dotnet-mstest.instructions.md', label: 'MSTest', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.nunit', fileName: 'dotnet-nunit.instructions.md', label: 'NUnit', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.csharp.codingStyle', fileName: 'dotnet-csharp-coding-style.instructions.md', label: 'Coding Style', category: 'C#' },
    { settingId: 'sharppilot.instructions.dotnet.fsharp.codingStyle', fileName: 'dotnet-fsharp-coding-style.instructions.md', label: 'Coding Style', category: 'F#' },
    { settingId: 'sharppilot.instructions.dotnet.vbnet.codingStyle', fileName: 'dotnet-vbnet-coding-style.instructions.md', label: 'Coding Style', category: 'VB.NET' },
    { settingId: 'sharppilot.instructions.git.commitFormat', fileName: 'git-commit-format.instructions.md', label: 'Commit Format', category: 'Git' },
    { settingId: 'sharppilot.instructions.scripting.powershell', fileName: 'scripting-powershell.instructions.md', label: 'PowerShell', category: 'Scripting' },
    { settingId: 'sharppilot.instructions.scripting.bash', fileName: 'scripting-bash.instructions.md', label: 'Bash', category: 'Scripting' },
    { settingId: 'sharppilot.instructions.scripting.batch', fileName: 'scripting-batch.instructions.md', label: 'Batch (CMD)', category: 'Scripting' },
    { settingId: 'sharppilot.instructions.web.angular', fileName: 'web-angular.instructions.md', label: 'Angular', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.css', fileName: 'web-css.instructions.md', label: 'CSS', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.html', fileName: 'web-html.instructions.md', label: 'HTML', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.javascript', fileName: 'web-javascript.instructions.md', label: 'JavaScript', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.nextJs', fileName: 'web-nextjs.instructions.md', label: 'Next.js', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.nodeJs', fileName: 'web-nodejs.instructions.md', label: 'Node.js', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.react', fileName: 'web-react.instructions.md', label: 'React', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.svelte', fileName: 'web-svelte.instructions.md', label: 'Svelte', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.typescript', fileName: 'web-typescript.instructions.md', label: 'TypeScript', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.vue', fileName: 'web-vue.instructions.md', label: 'Vue.js', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.testing', fileName: 'web-testing.instructions.md', label: 'Testing', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.vitest', fileName: 'web-vitest.instructions.md', label: 'Vitest', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.jest', fileName: 'web-jest.instructions.md', label: 'Jest', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.jasmine', fileName: 'web-jasmine.instructions.md', label: 'Jasmine', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.mocha', fileName: 'web-mocha.instructions.md', label: 'Mocha', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.playwright', fileName: 'web-playwright.instructions.md', label: 'Playwright', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.cypress', fileName: 'web-cypress.instructions.md', label: 'Cypress', category: 'Web' },
];

export const tools: readonly ToolEntry[] = [
    { settingId: 'sharppilot.tools.check_csharp_async_patterns', toolName: 'check_csharp_async_patterns', label: 'Async Patterns', category: '.NET Tool' },
    { settingId: 'sharppilot.tools.check_csharp_coding_style', toolName: 'check_csharp_coding_style', label: 'Coding Style', category: '.NET Tool' },
    { settingId: 'sharppilot.tools.check_csharp_member_ordering', toolName: 'check_csharp_member_ordering', label: 'Member Ordering', category: '.NET Tool' },
    { settingId: 'sharppilot.tools.check_csharp_naming_conventions', toolName: 'check_csharp_naming_conventions', label: 'Naming Conventions', category: '.NET Tool' },
    { settingId: 'sharppilot.tools.check_csharp_nullable_context', toolName: 'check_csharp_nullable_context', label: 'Nullable Context', category: '.NET Tool' },
    { settingId: 'sharppilot.tools.check_csharp_project_structure', toolName: 'check_csharp_project_structure', label: 'Project Structure', category: '.NET Tool' },
    { settingId: 'sharppilot.tools.check_csharp_test_style', toolName: 'check_csharp_test_style', label: 'Test Style', category: '.NET Tool' },
    { settingId: 'sharppilot.tools.check_nuget_hygiene', toolName: 'check_nuget_hygiene', label: 'NuGet Hygiene', category: '.NET Tool' },
    { settingId: 'sharppilot.tools.check_git_commit_content', toolName: 'check_git_commit_content', label: 'Commit Content', category: 'Git Tool' },
    { settingId: 'sharppilot.tools.check_git_commit_format', toolName: 'check_git_commit_format', label: 'Commit Format', category: 'Git Tool' },
    { settingId: 'sharppilot.tools.get_editorconfig', toolName: 'get_editorconfig', label: 'EditorConfig', category: 'EditorConfig Tool' },
];

export function toolSettingsForScope(scope: string): readonly string[] {
    const categoryPrefix: Record<string, string> = {
        dotnet: '.NET Tool',
        git: 'Git Tool',
        editorconfig: 'EditorConfig Tool',
    };
    const cat = categoryPrefix[scope];
    return cat ? tools.filter(t => t.category === cat).map(t => t.settingId) : [];
}

export interface ExportManifest {
    exports: Record<string, { hash: string }>;
}

const settingIdPrefix = 'sharppilot.instructions.';
const overrideContextPrefix = 'sharppilot.override.';
const filteredContextPrefix = 'sharppilot.filtered.';

export function overrideContextKey(settingId: string): string {
    return overrideContextPrefix + settingId.slice(settingIdPrefix.length);
}

export function filteredContextKey(settingId: string): string {
    return filteredContextPrefix + settingId.slice(settingIdPrefix.length);
}

export function targetPath(entry: InstructionEntry): string {
    return `.github/instructions/${entry.fileName}`;
}

const instructionsByFileName = new Map(instructions.map(i => [i.fileName, i]));

export function instructionByFileName(fileName: string): InstructionEntry | undefined {
    return instructionsByFileName.get(fileName);
}

const entryContextKeys = new Map<string, readonly string[]>([
    ['sharppilot.instructions.docker', ['hasDocker']],
    ['sharppilot.instructions.graphql', ['hasGraphql']],
    ['sharppilot.instructions.dotnet.aspnetCore', ['hasAspNetCore']],
    ['sharppilot.instructions.dotnet.asyncAwait', ['hasDotnet']],
    ['sharppilot.instructions.dotnet.blazor', ['hasBlazor']],
    ['sharppilot.instructions.dotnet.codingStandards', ['hasDotnet']],
    ['sharppilot.instructions.dotnet.core', ['hasDotnet']],
    ['sharppilot.instructions.dotnet.dapper', ['hasDapper']],
    ['sharppilot.instructions.dotnet.debugging', ['hasDotnet']],
    ['sharppilot.instructions.dotnet.entityFrameworkCore', ['hasEntityFrameworkCore']],
    ['sharppilot.instructions.dotnet.grpc', ['hasGrpc']],
    ['sharppilot.instructions.dotnet.maui', ['hasMaui']],
    ['sharppilot.instructions.dotnet.mediatorCqrs', ['hasMediatR']],
    ['sharppilot.instructions.dotnet.mongodb', ['hasMongodb']],
    ['sharppilot.instructions.dotnet.mysql', ['hasMysql']],
    ['sharppilot.instructions.dotnet.nuget', ['hasDotnet']],
    ['sharppilot.instructions.dotnet.oracle', ['hasOracle']],
    ['sharppilot.instructions.dotnet.performanceMemory', ['hasDotnet']],
    ['sharppilot.instructions.dotnet.postgresql', ['hasPostgres']],
    ['sharppilot.instructions.dotnet.razor', ['hasBlazor']],
    ['sharppilot.instructions.dotnet.redis', ['hasRedis']],
    ['sharppilot.instructions.dotnet.signalR', ['hasSignalR']],
    ['sharppilot.instructions.dotnet.sqlite', ['hasSqlite']],
    ['sharppilot.instructions.dotnet.sqlServer', ['hasSqlServer']],
    ['sharppilot.instructions.dotnet.testing', ['hasDotnet']],
    ['sharppilot.instructions.dotnet.unity', ['hasUnity']],
    ['sharppilot.instructions.dotnet.winForms', ['hasWinForms']],
    ['sharppilot.instructions.dotnet.wpf', ['hasWpf']],
    ['sharppilot.instructions.dotnet.xunit', ['hasXunit']],
    ['sharppilot.instructions.dotnet.mstest', ['hasMstest']],
    ['sharppilot.instructions.dotnet.nunit', ['hasNunit']],
    ['sharppilot.instructions.dotnet.csharp.codingStyle', ['hasDotnet']],
    ['sharppilot.instructions.dotnet.fsharp.codingStyle', ['hasFsharp']],
    ['sharppilot.instructions.dotnet.vbnet.codingStyle', ['hasVbnet']],
    ['sharppilot.instructions.git.commitFormat', ['hasGit']],
    ['sharppilot.instructions.scripting.powershell', ['hasPowerShell']],
    ['sharppilot.instructions.scripting.bash', ['hasBash']],
    ['sharppilot.instructions.scripting.batch', ['hasBatch']],
    ['sharppilot.instructions.web.angular', ['hasAngular']],
    ['sharppilot.instructions.web.css', ['hasCss']],
    ['sharppilot.instructions.web.html', ['hasHtml']],
    ['sharppilot.instructions.web.javascript', ['hasJavaScript', 'hasTypeScript']],
    ['sharppilot.instructions.web.nextJs', ['hasNextJs']],
    ['sharppilot.instructions.web.nodeJs', ['hasNodeJs']],
    ['sharppilot.instructions.web.react', ['hasReact']],
    ['sharppilot.instructions.web.svelte', ['hasSvelte']],
    ['sharppilot.instructions.web.typescript', ['hasTypeScript']],
    ['sharppilot.instructions.web.vue', ['hasVue']],
    ['sharppilot.instructions.web.testing', ['hasWebTesting']],
    ['sharppilot.instructions.web.vitest', ['hasVitest']],
    ['sharppilot.instructions.web.jest', ['hasJest']],
    ['sharppilot.instructions.web.jasmine', ['hasJasmine']],
    ['sharppilot.instructions.web.mocha', ['hasMocha']],
    ['sharppilot.instructions.web.playwright', ['hasPlaywright']],
    ['sharppilot.instructions.web.cypress', ['hasCypress']],
    ['sharppilot.tools.check_csharp_async_patterns', ['hasDotnet']],
    ['sharppilot.tools.check_csharp_coding_style', ['hasDotnet']],
    ['sharppilot.tools.check_csharp_member_ordering', ['hasDotnet']],
    ['sharppilot.tools.check_csharp_naming_conventions', ['hasDotnet']],
    ['sharppilot.tools.check_csharp_nullable_context', ['hasDotnet']],
    ['sharppilot.tools.check_csharp_project_structure', ['hasDotnet']],
    ['sharppilot.tools.check_csharp_test_style', ['hasDotnet']],
    ['sharppilot.tools.check_nuget_hygiene', ['hasDotnet']],
    ['sharppilot.tools.check_git_commit_content', ['hasGit']],
    ['sharppilot.tools.check_git_commit_format', ['hasGit']],
]);

export function contextKeysForEntry(entry: ToggleEntry): readonly string[] {
    return entryContextKeys.get(entry.settingId) ?? [];
}
