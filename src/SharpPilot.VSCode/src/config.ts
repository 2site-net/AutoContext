export interface ServerEntry {
    label: string;
    scope: string;
}

export interface InstructionEntry {
    settingId: string;
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
    { settingId: 'sharp-pilot.instructions.copilot', label: 'Copilot Rules', category: 'General' },
    { settingId: 'sharp-pilot.instructions.csharp.codingStyle', label: 'Coding Style', category: 'C#' },
    { settingId: 'sharp-pilot.instructions.dotnet.asyncAwait', label: 'Async/Await', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.blazor', label: 'Blazor', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.razor', label: 'Razor', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.html', label: 'HTML', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.dotnet.css', label: 'CSS', category: 'Web' },
    { settingId: 'sharp-pilot.instructions.dotnet.codingStandards', label: 'Coding Standards', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.debugging', label: 'Debugging', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.designPrinciples', label: 'Design Principles', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.nuget', label: 'NuGet', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.performanceMemory', label: 'Performance & Memory', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.testing', label: 'Testing', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.dotnet.xunit', label: 'xUnit', category: '.NET' },
    { settingId: 'sharp-pilot.instructions.git.commitFormat', label: 'Commit Format', category: 'Git' },
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
