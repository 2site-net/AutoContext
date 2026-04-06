import type { McpToolEntry } from './mcp-tool-entry.js';
import { McpToolsCatalog } from './mcp-tools-catalog.js';

export class McpToolsRegistry {
    private static readonly catalog = new McpToolsCatalog([
        { settingId: 'sharppilot.tools.check_csharp_async_patterns', featureName: 'check_csharp_async_patterns', toolName: 'check_csharp_all', label: 'Async Patterns', category: 'C#', group: '.NET', contextKeys: ['hasCSharp'] },
        { settingId: 'sharppilot.tools.check_csharp_coding_style', featureName: 'check_csharp_coding_style', toolName: 'check_csharp_all', label: 'Coding Style', category: 'C#', group: '.NET', contextKeys: ['hasCSharp'] },
        { settingId: 'sharppilot.tools.check_csharp_member_ordering', featureName: 'check_csharp_member_ordering', toolName: 'check_csharp_all', label: 'Member Ordering', category: 'C#', group: '.NET', contextKeys: ['hasCSharp'] },
        { settingId: 'sharppilot.tools.check_csharp_naming_conventions', featureName: 'check_csharp_naming_conventions', toolName: 'check_csharp_all', label: 'Naming Conventions', category: 'C#', group: '.NET', contextKeys: ['hasCSharp'] },
        { settingId: 'sharppilot.tools.check_csharp_nullable_context', featureName: 'check_csharp_nullable_context', toolName: 'check_csharp_all', label: 'Nullable Context', category: 'C#', group: '.NET', contextKeys: ['hasCSharp'] },
        { settingId: 'sharppilot.tools.check_csharp_project_structure', featureName: 'check_csharp_project_structure', toolName: 'check_csharp_all', label: 'Project Structure', category: 'C#', group: '.NET', contextKeys: ['hasCSharp'] },
        { settingId: 'sharppilot.tools.check_csharp_test_style', featureName: 'check_csharp_test_style', toolName: 'check_csharp_all', label: 'Test Style', category: 'C#', group: '.NET', contextKeys: ['hasCSharp'] },
        { settingId: 'sharppilot.tools.check_nuget_hygiene', toolName: 'check_nuget_hygiene', label: 'NuGet Hygiene', category: 'NuGet', group: '.NET', contextKeys: ['hasDotNet'] },
        { settingId: 'sharppilot.tools.check_git_commit_content', featureName: 'check_git_commit_content', toolName: 'check_git_all', label: 'Commit Content', category: 'Git', group: 'Workspace', contextKeys: ['hasGit'] },
        { settingId: 'sharppilot.tools.check_git_commit_format', featureName: 'check_git_commit_format', toolName: 'check_git_all', label: 'Commit Format', category: 'Git', group: 'Workspace', contextKeys: ['hasGit'] },
        { settingId: 'sharppilot.tools.get_editorconfig', toolName: 'get_editorconfig', label: 'EditorConfig', category: 'EditorConfig', group: 'Workspace' },
        { settingId: 'sharppilot.tools.check_typescript_coding_style', featureName: 'check_typescript_coding_style', toolName: 'check_typescript_all', label: 'Coding Style', category: 'TypeScript', group: 'Web', contextKeys: ['hasTypeScript'] },
    ]);

    static get all(): readonly McpToolEntry[] {
        return this.catalog.all;
    }

    static get count(): number {
        return this.catalog.count;
    }

    static getSettingIdByCategory(category: string): readonly string[] {
        return this.catalog.getSettingIdByCategory(category);
    }
}
