import type { ToolEntry } from './tool-entry.js';

const categories: Record<string, string> = {
    dotnet: '.NET Tool',
    git: 'Git Tool',
    editorconfig: 'EditorConfig Tool',
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
        const cat = categories[category];
        return cat ? this.entries.filter(t => t.category === cat).map(t => t.settingId) : [];
    }
}

export const toolsCatalog = new ToolsCatalog([
    { settingId: 'sharppilot.tools.check_csharp_async_patterns', toolName: 'check_csharp_async_patterns', label: 'Async Patterns', category: '.NET Tool', contextKeys: ['hasDotnet'] },
    { settingId: 'sharppilot.tools.check_csharp_coding_style', toolName: 'check_csharp_coding_style', label: 'Coding Style', category: '.NET Tool', contextKeys: ['hasDotnet'] },
    { settingId: 'sharppilot.tools.check_csharp_member_ordering', toolName: 'check_csharp_member_ordering', label: 'Member Ordering', category: '.NET Tool', contextKeys: ['hasDotnet'] },
    { settingId: 'sharppilot.tools.check_csharp_naming_conventions', toolName: 'check_csharp_naming_conventions', label: 'Naming Conventions', category: '.NET Tool', contextKeys: ['hasDotnet'] },
    { settingId: 'sharppilot.tools.check_csharp_nullable_context', toolName: 'check_csharp_nullable_context', label: 'Nullable Context', category: '.NET Tool', contextKeys: ['hasDotnet'] },
    { settingId: 'sharppilot.tools.check_csharp_project_structure', toolName: 'check_csharp_project_structure', label: 'Project Structure', category: '.NET Tool', contextKeys: ['hasDotnet'] },
    { settingId: 'sharppilot.tools.check_csharp_test_style', toolName: 'check_csharp_test_style', label: 'Test Style', category: '.NET Tool', contextKeys: ['hasDotnet'] },
    { settingId: 'sharppilot.tools.check_nuget_hygiene', toolName: 'check_nuget_hygiene', label: 'NuGet Hygiene', category: '.NET Tool', contextKeys: ['hasDotnet'] },
    { settingId: 'sharppilot.tools.check_git_commit_content', toolName: 'check_git_commit_content', label: 'Commit Content', category: 'Git Tool', contextKeys: ['hasGit'] },
    { settingId: 'sharppilot.tools.check_git_commit_format', toolName: 'check_git_commit_format', label: 'Commit Format', category: 'Git Tool', contextKeys: ['hasGit'] },
    { settingId: 'sharppilot.tools.get_editorconfig', toolName: 'get_editorconfig', label: 'EditorConfig', category: 'EditorConfig Tool' },
]);
