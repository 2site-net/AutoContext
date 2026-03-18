export interface ServerEntry {
    label: string;
    scope: string;
}

export interface InstructionEntry {
    settingId: string;
    fileName: string;
    version: number;
    label: string;
    category: string;
}

export interface ToolEntry {
    settingId: string;
    toolName: string;
    label: string;
    category: string;
}

export const servers: readonly ServerEntry[] = [
    { label: 'SharpPilot: DotNet', scope: 'dotnet' },
    { label: 'SharpPilot: Git', scope: 'git' },
];

export const instructions: readonly InstructionEntry[] = [
    { settingId: 'sharp-pilot.instructions.dotnet.aspnetCore', fileName: 'dotnet-aspnetcore.instructions.md', version: 1, label: 'ASP.NET Core', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.asyncAwait', fileName: 'dotnet-async-await.instructions.md', version: 1, label: 'Async/Await', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.blazor', fileName: 'dotnet-blazor.instructions.md', version: 1, label: 'Blazor', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.codingStandards', fileName: 'dotnet-coding-standards.instructions.md', version: 1, label: 'Coding Standards', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.dapper', fileName: 'dotnet-dapper.instructions.md', version: 1, label: 'Dapper', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.debugging', fileName: 'dotnet-debugging.instructions.md', version: 1, label: 'Debugging', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.entityFrameworkCore', fileName: 'dotnet-entity-framework-core.instructions.md', version: 1, label: 'Entity Framework Core', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.maui', fileName: 'dotnet-maui.instructions.md', version: 1, label: '.NET MAUI', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.mongodb', fileName: 'dotnet-mongodb.instructions.md', version: 1, label: 'MongoDB', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.mysql', fileName: 'dotnet-mysql.instructions.md', version: 1, label: 'MySQL', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.nuget', fileName: 'dotnet-nuget.instructions.md', version: 1, label: 'NuGet', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.oracle', fileName: 'dotnet-oracle.instructions.md', version: 1, label: 'Oracle', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.performanceMemory', fileName: 'dotnet-performance-memory.instructions.md', version: 1, label: 'Performance & Memory', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.postgresql', fileName: 'dotnet-postgresql.instructions.md', version: 1, label: 'PostgreSQL', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.razor', fileName: 'dotnet-razor.instructions.md', version: 1, label: 'Razor', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.sqlite', fileName: 'dotnet-sqlite.instructions.md', version: 1, label: 'SQLite', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.sqlServer', fileName: 'dotnet-sql-server.instructions.md', version: 1, label: 'SQL Server', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.testing', fileName: 'dotnet-testing.instructions.md', version: 1, label: 'Testing', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.unity', fileName: 'dotnet-unity.instructions.md', version: 1, label: 'Unity', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.winForms', fileName: 'dotnet-winforms.instructions.md', version: 1, label: 'Windows Forms', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.wpf', fileName: 'dotnet-wpf.instructions.md', version: 1, label: 'WPF', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.xunit', fileName: 'dotnet-xunit.instructions.md', version: 1, label: 'xUnit', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.csharp.codingStyle', fileName: 'dotnet-csharp-coding-style.instructions.md', version: 1, label: 'Coding Style', category: 'C#' },
    { settingId: 'sharp-pilot.instructions.dotnet.fsharp.codingStyle', fileName: 'dotnet-fsharp-coding-style.instructions.md', version: 1, label: 'Coding Style', category: 'F#' },
    { settingId: 'sharp-pilot.instructions.copilot', fileName: 'copilot.instructions.md', version: 1, label: 'Copilot Rules', category: 'General' },
    { settingId: 'sharp-pilot.instructions.designPrinciples', fileName: 'design-principles.instructions.md', version: 1, label: 'Design Principles', category: 'General' },
    { settingId: 'sharp-pilot.instructions.git.commitFormat', fileName: 'git-commit-format.instructions.md', version: 1, label: 'Commit Format', category: 'Git' },
    { settingId: 'sharp-pilot.instructions.web.angular', fileName: 'web-angular.instructions.md', version: 1, label: 'Angular', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.css', fileName: 'web-css.instructions.md', version: 1, label: 'CSS', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.html', fileName: 'web-html.instructions.md', version: 1, label: 'HTML', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.javascript', fileName: 'web-javascript.instructions.md', version: 1, label: 'JavaScript', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.react', fileName: 'web-react.instructions.md', version: 1, label: 'React', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.svelte', fileName: 'web-svelte.instructions.md', version: 1, label: 'Svelte', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.typescript', fileName: 'web-typescript.instructions.md', version: 1, label: 'TypeScript', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.web.vue', fileName: 'web-vue.instructions.md', version: 1, label: 'Vue.js', category: 'Web' },
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
];

export interface ExportManifest {
    exports: Record<string, { version: number }>;
}

const settingIdPrefix = 'sharp-pilot.instructions.';
const overrideContextPrefix = 'sharp-pilot.override.';

export function overrideContextKey(settingId: string): string {
    return overrideContextPrefix + settingId.slice(settingIdPrefix.length);
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
