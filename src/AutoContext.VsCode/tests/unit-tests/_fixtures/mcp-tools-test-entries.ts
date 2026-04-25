import type { McpCategory } from '../../../src/types/mcp-category';
import type { McpToolsEntry } from '../../../src/types/mcp-tools-entry';

const dotnet: McpCategory = { name: '.NET', workerId: 'dotnet' };
const workspace: McpCategory = { name: 'Workspace', workerId: 'workspace' };
const csharp: McpCategory = { name: 'C#' };
const nuget: McpCategory = { name: 'NuGet' };
const git: McpCategory = { name: 'Git' };

export const mcpToolsTestEntries: readonly McpToolsEntry[] = [
    { key: 'alpha', label: 'Alpha', leafCategory: csharp, workerCategory: dotnet },
    { key: 'beta', label: 'Beta', leafCategory: nuget, workerCategory: dotnet },
    { key: 'gamma', label: 'Gamma', leafCategory: git, workerCategory: workspace },
];
