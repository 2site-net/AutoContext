import type { InstructionsFileEntry } from './instructions-file-entry.js';

export class ContextKeys {
    private static readonly contextKeyPrefix = 'autocontext.instructions.';
    private static readonly overrideContextPrefix = 'autocontext.override.';

    static overrideKey(contextKey: string): string {
        return ContextKeys.overrideContextPrefix + contextKey.slice(ContextKeys.contextKeyPrefix.length);
    }

    static forEntry(entry: InstructionsFileEntry): readonly string[] {
        return entry.activationFlags ?? [];
    }
}
