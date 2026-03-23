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
    { settingId: 'sharp-pilot.instructions.copilot', fileName: 'copilot.instructions.md', label: 'Copilot Rules', category: 'General' },
    { settingId: 'sharp-pilot.instructions.codeReview', fileName: 'code-review.instructions.md', label: 'Code Review', category: 'General' },
    { settingId: 'sharp-pilot.instructions.designPrinciples', fileName: 'design-principles.instructions.md', label: 'Design Principles', category: 'General' },
    { settingId: 'sharp-pilot.instructions.docker', fileName: 'docker.instructions.md', label: 'Docker', category: 'General' },
    { settingId: 'sharp-pilot.instructions.graphql', fileName: 'graphql.instructions.md', label: 'GraphQL', category: 'General' },
    { settingId: 'sharp-pilot.instructions.restApiDesign', fileName: 'rest-api-design.instructions.md', label: 'REST API Design', category: 'General' },
    { settingId: 'sharp-pilot.instructions.sql', fileName: 'sql.instructions.md', label: 'SQL', category: 'General' },
    { settingId: 'sharp-pilot.instructions.dotnet.aspnetCore', fileName: 'dotnet-aspnetcore.instructions.md', label: 'ASP.NET Core', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.asyncAwait', fileName: 'dotnet-async-await.instructions.md', label: 'Async/Await', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.blazor', fileName: 'dotnet-blazor.instructions.md', label: 'Blazor', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.codingStandards', fileName: 'dotnet-coding-standards.instructions.md', label: 'Coding Standards', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.core', fileName: 'dotnet-core.instructions.md', label: 'Core (DI, Logging, Config, Security)', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.dapper', fileName: 'dotnet-dapper.instructions.md', label: 'Dapper', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.debugging', fileName: 'dotnet-debugging.instructions.md', label: 'Debugging', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.entityFrameworkCore', fileName: 'dotnet-entity-framework-core.instructions.md', label: 'Entity Framework Core', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.grpc', fileName: 'dotnet-grpc.instructions.md', label: 'gRPC', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.maui', fileName: 'dotnet-maui.instructions.md', label: '.NET MAUI', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.mediatorCqrs', fileName: 'dotnet-mediator-cqrs.instructions.md', label: 'Mediator / CQRS', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.mongodb', fileName: 'dotnet-mongodb.instructions.md', label: 'MongoDB', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.mysql', fileName: 'dotnet-mysql.instructions.md', label: 'MySQL', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.nuget', fileName: 'dotnet-nuget.instructions.md', label: 'NuGet', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.oracle', fileName: 'dotnet-oracle.instructions.md', label: 'Oracle', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.performanceMemory', fileName: 'dotnet-performance-memory.instructions.md', label: 'Performance & Memory', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.postgresql', fileName: 'dotnet-postgresql.instructions.md', label: 'PostgreSQL', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.razor', fileName: 'dotnet-razor.instructions.md', label: 'Razor', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.redis', fileName: 'dotnet-redis.instructions.md', label: 'Redis', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.signalR', fileName: 'dotnet-signalr.instructions.md', label: 'SignalR', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.sqlite', fileName: 'dotnet-sqlite.instructions.md', label: 'SQLite', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.sqlServer', fileName: 'dotnet-sql-server.instructions.md', label: 'SQL Server', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.testing', fileName: 'dotnet-testing.instructions.md', label: 'Testing', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.unity', fileName: 'dotnet-unity.instructions.md', label: 'Unity', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.winForms', fileName: 'dotnet-winforms.instructions.md', label: 'Windows Forms', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.wpf', fileName: 'dotnet-wpf.instructions.md', label: 'WPF', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.xunit', fileName: 'dotnet-xunit.instructions.md', label: 'xUnit', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.mstest', fileName: 'dotnet-mstest.instructions.md', label: 'MSTest', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.nunit', fileName: 'dotnet-nunit.instructions.md', label: 'NUnit', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.csharp.codingStyle', fileName: 'dotnet-csharp-coding-style.instructions.md', label: 'Coding Style', category: 'C#' },
    { settingId: 'sharp-pilot.instructions.dotnet.fsharp.codingStyle', fileName: 'dotnet-fsharp-coding-style.instructions.md', label: 'Coding Style', category: 'F#' },
    { settingId: 'sharp-pilot.instructions.dotnet.vbnet.codingStyle', fileName: 'dotnet-vbnet-coding-style.instructions.md', label: 'Coding Style', category: 'VB.NET' },
    { settingId: 'sharp-pilot.instructions.git.commitFormat', fileName: 'git-commit-format.instructions.md', label: 'Commit Format', category: 'Git' },
    { settingId: 'sharp-pilot.instructions.scripting.powershell', fileName: 'scripting-powershell.instructions.md', label: 'PowerShell', category: 'Scripting' },
    { settingId: 'sharp-pilot.instructions.scripting.bash', fileName: 'scripting-bash.instructions.md', label: 'Bash', category: 'Scripting' },
    { settingId: 'sharp-pilot.instructions.scripting.batch', fileName: 'scripting-batch.instructions.md', label: 'Batch (CMD)', category: 'Scripting' },
    { settingId: 'sharp-pilot.instructions.web.angular', fileName: 'web-angular.instructions.md', label: 'Angular', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.css', fileName: 'web-css.instructions.md', label: 'CSS', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.html', fileName: 'web-html.instructions.md', label: 'HTML', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.javascript', fileName: 'web-javascript.instructions.md', label: 'JavaScript', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.nextJs', fileName: 'web-nextjs.instructions.md', label: 'Next.js', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.nodeJs', fileName: 'web-nodejs.instructions.md', label: 'Node.js', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.react', fileName: 'web-react.instructions.md', label: 'React', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.svelte', fileName: 'web-svelte.instructions.md', label: 'Svelte', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.typescript', fileName: 'web-typescript.instructions.md', label: 'TypeScript', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.vue', fileName: 'web-vue.instructions.md', label: 'Vue.js', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.testing', fileName: 'web-testing.instructions.md', label: 'Testing', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.vitest', fileName: 'web-vitest.instructions.md', label: 'Vitest', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.jest', fileName: 'web-jest.instructions.md', label: 'Jest', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.jasmine', fileName: 'web-jasmine.instructions.md', label: 'Jasmine', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.mocha', fileName: 'web-mocha.instructions.md', label: 'Mocha', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.playwright', fileName: 'web-playwright.instructions.md', label: 'Playwright', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.cypress', fileName: 'web-cypress.instructions.md', label: 'Cypress', category: 'Web' },
];

export const tools: readonly ToolEntry[] = [
    { settingId: 'sharp-pilot.tools.check_csharp_async_patterns', toolName: 'check_csharp_async_patterns', label: 'Async Patterns', category: '.NET Tool' },
    { settingId: 'sharp-pilot.tools.check_csharp_coding_style', toolName: 'check_csharp_coding_style', label: 'Coding Style', category: '.NET Tool' },
    { settingId: 'sharp-pilot.tools.check_csharp_member_ordering', toolName: 'check_csharp_member_ordering', label: 'Member Ordering', category: '.NET Tool' },
    { settingId: 'sharp-pilot.tools.check_csharp_naming_conventions', toolName: 'check_csharp_naming_conventions', label: 'Naming Conventions', category: '.NET Tool' },
    { settingId: 'sharp-pilot.tools.check_csharp_nullable_context', toolName: 'check_csharp_nullable_context', label: 'Nullable Context', category: '.NET Tool' },
    { settingId: 'sharp-pilot.tools.check_csharp_project_structure', toolName: 'check_csharp_project_structure', label: 'Project Structure', category: '.NET Tool' },
    { settingId: 'sharp-pilot.tools.check_csharp_test_style', toolName: 'check_csharp_test_style', label: 'Test Style', category: '.NET Tool' },
    { settingId: 'sharp-pilot.tools.check_nuget_hygiene', toolName: 'check_nuget_hygiene', label: 'NuGet Hygiene', category: '.NET Tool' },
    { settingId: 'sharp-pilot.tools.check_git_commit_content', toolName: 'check_git_commit_content', label: 'Commit Content', category: 'Git Tool' },
    { settingId: 'sharp-pilot.tools.check_git_commit_format', toolName: 'check_git_commit_format', label: 'Commit Format', category: 'Git Tool' },
    { settingId: 'sharp-pilot.tools.get_editorconfig', toolName: 'get_editorconfig', label: 'EditorConfig', category: 'EditorConfig Tool' },
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

const settingIdPrefix = 'sharp-pilot.instructions.';
const overrideContextPrefix = 'sharp-pilot.override.';
const filteredContextPrefix = 'sharp-pilot.filtered.';

export function overrideContextKey(settingId: string): string {
    return overrideContextPrefix + settingId.slice(settingIdPrefix.length);
}

export function filteredContextKey(settingId: string): string {
    return filteredContextPrefix + settingId.slice(settingIdPrefix.length);
}

export function targetPath(entry: InstructionEntry): string {
    return entry.fileName === 'copilot.instructions.md'
        ? '.github/copilot-instructions.md'
        : `.github/instructions/${entry.fileName}`;
}

const instructionsByFileName = new Map(instructions.map(i => [i.fileName, i]));

export function instructionByFileName(fileName: string): InstructionEntry | undefined {
    return instructionsByFileName.get(fileName);
}

const entryContextKeys = new Map<string, readonly string[]>([
    ['sharp-pilot.instructions.docker', ['hasDocker']],
    ['sharp-pilot.instructions.graphql', ['hasGraphql']],
    ['sharp-pilot.instructions.dotnet.aspnetCore', ['hasAspNetCore']],
    ['sharp-pilot.instructions.dotnet.asyncAwait', ['hasDotnet']],
    ['sharp-pilot.instructions.dotnet.blazor', ['hasBlazor']],
    ['sharp-pilot.instructions.dotnet.codingStandards', ['hasDotnet']],
    ['sharp-pilot.instructions.dotnet.core', ['hasDotnet']],
    ['sharp-pilot.instructions.dotnet.dapper', ['hasDapper']],
    ['sharp-pilot.instructions.dotnet.debugging', ['hasDotnet']],
    ['sharp-pilot.instructions.dotnet.entityFrameworkCore', ['hasEntityFrameworkCore']],
    ['sharp-pilot.instructions.dotnet.grpc', ['hasGrpc']],
    ['sharp-pilot.instructions.dotnet.maui', ['hasMaui']],
    ['sharp-pilot.instructions.dotnet.mediatorCqrs', ['hasMediatR']],
    ['sharp-pilot.instructions.dotnet.mongodb', ['hasMongodb']],
    ['sharp-pilot.instructions.dotnet.mysql', ['hasMysql']],
    ['sharp-pilot.instructions.dotnet.nuget', ['hasDotnet']],
    ['sharp-pilot.instructions.dotnet.oracle', ['hasOracle']],
    ['sharp-pilot.instructions.dotnet.performanceMemory', ['hasDotnet']],
    ['sharp-pilot.instructions.dotnet.postgresql', ['hasPostgres']],
    ['sharp-pilot.instructions.dotnet.razor', ['hasBlazor']],
    ['sharp-pilot.instructions.dotnet.redis', ['hasRedis']],
    ['sharp-pilot.instructions.dotnet.signalR', ['hasSignalR']],
    ['sharp-pilot.instructions.dotnet.sqlite', ['hasSqlite']],
    ['sharp-pilot.instructions.dotnet.sqlServer', ['hasSqlServer']],
    ['sharp-pilot.instructions.dotnet.testing', ['hasDotnet']],
    ['sharp-pilot.instructions.dotnet.unity', ['hasUnity']],
    ['sharp-pilot.instructions.dotnet.winForms', ['hasWinForms']],
    ['sharp-pilot.instructions.dotnet.wpf', ['hasWpf']],
    ['sharp-pilot.instructions.dotnet.xunit', ['hasXunit']],
    ['sharp-pilot.instructions.dotnet.mstest', ['hasMstest']],
    ['sharp-pilot.instructions.dotnet.nunit', ['hasNunit']],
    ['sharp-pilot.instructions.dotnet.csharp.codingStyle', ['hasDotnet']],
    ['sharp-pilot.instructions.dotnet.fsharp.codingStyle', ['hasFsharp']],
    ['sharp-pilot.instructions.dotnet.vbnet.codingStyle', ['hasVbnet']],
    ['sharp-pilot.instructions.git.commitFormat', ['hasGit']],
    ['sharp-pilot.instructions.scripting.powershell', ['hasPowerShell']],
    ['sharp-pilot.instructions.scripting.bash', ['hasBash']],
    ['sharp-pilot.instructions.scripting.batch', ['hasBatch']],
    ['sharp-pilot.instructions.web.angular', ['hasAngular']],
    ['sharp-pilot.instructions.web.css', ['hasCss']],
    ['sharp-pilot.instructions.web.html', ['hasHtml']],
    ['sharp-pilot.instructions.web.javascript', ['hasJavaScript', 'hasTypeScript']],
    ['sharp-pilot.instructions.web.nextJs', ['hasNextJs']],
    ['sharp-pilot.instructions.web.nodeJs', ['hasNodeJs']],
    ['sharp-pilot.instructions.web.react', ['hasReact']],
    ['sharp-pilot.instructions.web.svelte', ['hasSvelte']],
    ['sharp-pilot.instructions.web.typescript', ['hasTypeScript']],
    ['sharp-pilot.instructions.web.vue', ['hasVue']],
    ['sharp-pilot.instructions.web.testing', ['hasWebTesting']],
    ['sharp-pilot.instructions.web.vitest', ['hasVitest']],
    ['sharp-pilot.instructions.web.jest', ['hasJest']],
    ['sharp-pilot.instructions.web.jasmine', ['hasJasmine']],
    ['sharp-pilot.instructions.web.mocha', ['hasMocha']],
    ['sharp-pilot.instructions.web.playwright', ['hasPlaywright']],
    ['sharp-pilot.instructions.web.cypress', ['hasCypress']],
    ['sharp-pilot.tools.check_csharp_async_patterns', ['hasDotnet']],
    ['sharp-pilot.tools.check_csharp_coding_style', ['hasDotnet']],
    ['sharp-pilot.tools.check_csharp_member_ordering', ['hasDotnet']],
    ['sharp-pilot.tools.check_csharp_naming_conventions', ['hasDotnet']],
    ['sharp-pilot.tools.check_csharp_nullable_context', ['hasDotnet']],
    ['sharp-pilot.tools.check_csharp_project_structure', ['hasDotnet']],
    ['sharp-pilot.tools.check_csharp_test_style', ['hasDotnet']],
    ['sharp-pilot.tools.check_nuget_hygiene', ['hasDotnet']],
    ['sharp-pilot.tools.check_git_commit_content', ['hasGit']],
    ['sharp-pilot.tools.check_git_commit_format', ['hasGit']],
]);

export function contextKeysForEntry(entry: ToggleEntry): readonly string[] {
    return entryContextKeys.get(entry.settingId) ?? [];
}
