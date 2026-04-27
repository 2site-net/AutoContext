import type { InstructionsFileEntry } from '../../../src/instructions-file-entry';
import { makeInstructionsFileEntry } from './make-entry';

export const detectorTestInstructions: InstructionsFileEntry[] = [
    makeInstructionsFileEntry('copilot.instructions.md', 'Copilot', ['general']),
    makeInstructionsFileEntry('dotnet-coding-standards.instructions.md', '.NET Standards', ['dotnet'], ['hasDotNet']),
];
