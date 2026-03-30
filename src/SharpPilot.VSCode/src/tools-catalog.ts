import type { ToolEntry } from './tool-entry.js';

const categories: Record<string, readonly string[]> = {
    dotnet: ['.NET', 'C#'],
    git: ['Git'],
    editorconfig: ['EditorConfig'],
    typescript: ['TypeScript'],
};

class ToolsCatalog {
    private readonly entries: readonly ToolEntry[];

    constructor(data: readonly ToolEntry[]) {
        this.entries = data;
    }

    get all(): readonly ToolEntry[] {
        return this.entries;
    }

    get count(): number {
        return this.entries.length;
    }

    getSettingIdByCategory(category: string): readonly string[] {
        const cats = categories[category];
        return cats ? this.entries.filter(t => cats.includes(t.category)).map(t => t.settingId) : [];
    }
}

export const toolsCatalog = new ToolsCatalog([
    { settingId: 'sharppilot.tools.check_csharp_async_patterns', toolName: 'check_csharp_async_patterns', label: 'Async Patterns', category: 'C#', contextKeys: ['hasCSharp'] },
    { settingId: 'sharppilot.tools.check_csharp_coding_style', toolName: 'check_csharp_coding_style', label: 'Coding Style', category: 'C#', contextKeys: ['hasCSharp'] },
    { settingId: 'sharppilot.tools.check_csharp_member_ordering', toolName: 'check_csharp_member_ordering', label: 'Member Ordering', category: 'C#', contextKeys: ['hasCSharp'] },
    { settingId: 'sharppilot.tools.check_csharp_naming_conventions', toolName: 'check_csharp_naming_conventions', label: 'Naming Conventions', category: 'C#', contextKeys: ['hasCSharp'] },
    { settingId: 'sharppilot.tools.check_csharp_nullable_context', toolName: 'check_csharp_nullable_context', label: 'Nullable Context', category: 'C#', contextKeys: ['hasCSharp'] },
    { settingId: 'sharppilot.tools.check_csharp_project_structure', toolName: 'check_csharp_project_structure', label: 'Project Structure', category: 'C#', contextKeys: ['hasCSharp'] },
    { settingId: 'sharppilot.tools.check_csharp_test_style', toolName: 'check_csharp_test_style', label: 'Test Style', category: 'C#', contextKeys: ['hasCSharp'] },
    { settingId: 'sharppilot.tools.check_nuget_hygiene', toolName: 'check_nuget_hygiene', label: 'NuGet Hygiene', category: '.NET', contextKeys: ['hasDotNet'] },
    { settingId: 'sharppilot.tools.check_git_commit_content', toolName: 'check_git_commit_content', label: 'Commit Content', category: 'Git', contextKeys: ['hasGit'] },
    { settingId: 'sharppilot.tools.check_git_commit_format', toolName: 'check_git_commit_format', label: 'Commit Format', category: 'Git', contextKeys: ['hasGit'] },
    { settingId: 'sharppilot.tools.get_editorconfig', toolName: 'get_editorconfig', label: 'EditorConfig', category: 'EditorConfig' },
    { settingId: 'sharppilot.tools.check_typescript_coding_style', toolName: 'check_typescript_coding_style', label: 'Coding Style', category: 'TypeScript', contextKeys: ['hasTypeScript'] },
]);
