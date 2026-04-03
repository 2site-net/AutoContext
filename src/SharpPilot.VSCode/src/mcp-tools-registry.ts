import type { McpToolEntry } from './mcp-tool-entry.js';
import { McpToolsCatalog } from './mcp-tools-catalog.js';

export class McpToolsRegistry {
    private static readonly catalog = new McpToolsCatalog([
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
