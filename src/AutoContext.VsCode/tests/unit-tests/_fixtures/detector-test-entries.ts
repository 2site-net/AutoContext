import type { InstructionsFileEntry } from '../../../src/types/instructions-file-entry';

export const detectorTestInstructions: InstructionsFileEntry[] = [
    { key: 'copilot', fileName: 'copilot.instructions.md', label: 'Copilot', category: 'general' },
    { key: 'dotnet.codingStandards', fileName: 'dotnet-coding-standards.instructions.md', label: '.NET Standards', category: 'dotnet', workspaceFlags: ['hasDotNet'] },
];
