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
    { label: 'QA-MCP: DotNet', scope: 'dotnet' },
    { label: 'QA-MCP: Git', scope: 'git' },
];

export const instructions: readonly InstructionEntry[] = [
    { settingId: 'qa-mcp.instructions.copilot', label: 'Copilot Rules', category: 'General' },
    { settingId: 'qa-mcp.instructions.csharp.codingStyle', label: 'Coding Style', category: 'C#' },
    { settingId: 'qa-mcp.instructions.dotnet.asyncAwait', label: 'Async/Await', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.blazor', label: 'Blazor', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.codingStandards', label: 'Coding Standards', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.debugging', label: 'Debugging', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.designPrinciples', label: 'Design Principles', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.nuget', label: 'NuGet', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.performanceMemory', label: 'Performance & Memory', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.testing', label: 'Testing', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.xunit', label: 'xUnit', category: '.NET' },
    { settingId: 'qa-mcp.instructions.git.commitFormat', label: 'Commit Format', category: 'Git' },
];

export const tools: readonly ToolEntry[] = [
    { settingId: 'qa-mcp.tools.check_async_patterns', toolName: 'check_async_patterns', label: 'Async Patterns', category: '.NET Tool' },
    { settingId: 'qa-mcp.tools.check_code_style', toolName: 'check_code_style', label: 'Code Style', category: '.NET Tool' },
    { settingId: 'qa-mcp.tools.check_member_ordering', toolName: 'check_member_ordering', label: 'Member Ordering', category: '.NET Tool' },
    { settingId: 'qa-mcp.tools.check_naming_conventions', toolName: 'check_naming_conventions', label: 'Naming Conventions', category: '.NET Tool' },
    { settingId: 'qa-mcp.tools.check_nuget_hygiene', toolName: 'check_nuget_hygiene', label: 'NuGet Hygiene', category: '.NET Tool' },
    { settingId: 'qa-mcp.tools.check_nullable_context', toolName: 'check_nullable_context', label: 'Nullable Context', category: '.NET Tool' },
    { settingId: 'qa-mcp.tools.check_project_structure', toolName: 'check_project_structure', label: 'Project Structure', category: '.NET Tool' },
    { settingId: 'qa-mcp.tools.check_tests_style', toolName: 'check_tests_style', label: 'Test Style', category: '.NET Tool' },
    { settingId: 'qa-mcp.tools.check_commit_content', toolName: 'validate_commit_content', label: 'Commit Content', category: 'Git Tool' },
    { settingId: 'qa-mcp.tools.check_commit_format', toolName: 'validate_commit_format', label: 'Commit Format', category: 'Git Tool' },
];
