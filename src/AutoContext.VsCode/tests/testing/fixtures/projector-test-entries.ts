import type { InstructionsFileEntry } from '../../../src/instructions-file-entry';
import { makeInstructionsFileEntry } from './make-entry';

export const projectorTestInstructions: InstructionsFileEntry[] = [
    makeInstructionsFileEntry('code-review.instructions.md', 'Code Review', ['General']),
    makeInstructionsFileEntry('lang-csharp.instructions.md', 'C#', ['Languages'], ['hasCSharp']),
];
