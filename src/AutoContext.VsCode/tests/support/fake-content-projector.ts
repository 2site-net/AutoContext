import { vi } from 'vitest';
import type { InstructionsFileContentProjector } from '../../src/instructions-file-content-projector';

export function createFakeContentProjector(): InstructionsFileContentProjector {
    return {
        project: vi.fn(async () => undefined),
    } as unknown as InstructionsFileContentProjector;
}
