import { vi } from 'vitest';
import type { InstructionsFilesLmToolsContentSearch } from '../../../src/instructions-files-lm-tools-content-search';

export function createFakeContentSearch(): InstructionsFilesLmToolsContentSearch {
    return {
        search: vi.fn(async () => []),
        dispose: vi.fn(),
    } as unknown as InstructionsFilesLmToolsContentSearch;
}
