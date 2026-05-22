import { makeInstructionsFileEntry } from './make-entry';

export const catalogTestEntries = [
    makeInstructionsFileEntry('alpha.instructions.md', 'Alpha', ['General']),
    makeInstructionsFileEntry('beta.instructions.md', 'Beta', ['.NET']),
    makeInstructionsFileEntry('gamma.instructions.md', 'Gamma', ['Web']),
];
