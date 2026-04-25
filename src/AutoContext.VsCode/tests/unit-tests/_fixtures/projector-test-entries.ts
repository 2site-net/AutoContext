import type { InstructionsFileEntry } from '../../../src/types/instructions-file-entry';
import type { McpToolsEntry } from '../../../src/types/mcp-tools-entry';

export const projectorTestInstructions: InstructionsFileEntry[] = [
    { key: 'codeReview', fileName: 'code-review.instructions.md', label: 'Code Review', category: 'General' },
    { key: 'lang.csharp', fileName: 'lang-csharp.instructions.md', label: 'C#', category: 'Languages', workspaceFlags: ['hasCSharp'] },
];

export const projectorTestTools: McpToolsEntry[] = [
    { key: 'analyze_csharp_coding_style', toolName: 'analyze_csharp_code', label: 'C# Coding Style', category: '.NET', serverLabel: '.NET', scope: 'dotnet', workspaceFlags: ['hasCSharp'] },
    { key: 'analyze_csharp_async_patterns', toolName: 'analyze_csharp_code', label: 'C# Async', category: '.NET', serverLabel: '.NET', scope: 'dotnet', workspaceFlags: ['hasCSharp'] },
    { key: 'get_editorconfig_rules', toolName: 'read_editorconfig_properties', label: 'EditorConfig', category: 'Workspace', serverLabel: 'Workspace', scope: 'editorconfig' },
];
