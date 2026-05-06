import { vi } from 'vitest';
import type { InstructionsFilesLmToolsApplyToMatcher } from '../../../src/instructions-files-lm-tools-apply-to-matcher';

export function createFakeApplyToMatcher(): InstructionsFilesLmToolsApplyToMatcher {
    return {
        matches: vi.fn(async (_input: string, _applyTo: string) => true),
    } as unknown as InstructionsFilesLmToolsApplyToMatcher;
}
