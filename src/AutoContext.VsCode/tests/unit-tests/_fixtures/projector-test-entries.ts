import type { InstructionsFileEntry } from '../../../src/types/instructions-file-entry';
import type { McpCategory } from '../../../src/types/mcp-category';
import type { McpToolsEntry } from '../../../src/types/mcp-tools-entry';

export const projectorTestInstructions: InstructionsFileEntry[] = [
    { key: 'codeReview', fileName: 'code-review.instructions.md', label: 'Code Review', category: 'General' },
    { key: 'lang.csharp', fileName: 'lang-csharp.instructions.md', label: 'C#', category: 'Languages', workspaceFlags: ['hasCSharp'] },
];

const dotnet: McpCategory = { name: '.NET', workerId: 'dotnet' };
const workspace: McpCategory = { name: 'Workspace', workerId: 'workspace' };

export const projectorTestTools: McpToolsEntry[] = [
    { key: 'analyze_csharp_coding_style', toolName: 'analyze_csharp_code', label: 'C# Coding Style', leafCategory: dotnet, workerCategory: dotnet, workspaceFlags: ['hasCSharp'] },
    { key: 'analyze_csharp_async_patterns', toolName: 'analyze_csharp_code', label: 'C# Async', leafCategory: dotnet, workerCategory: dotnet, workspaceFlags: ['hasCSharp'] },
    { key: 'get_editorconfig_rules', toolName: 'read_editorconfig_properties', label: 'EditorConfig', leafCategory: workspace, workerCategory: workspace },
];
