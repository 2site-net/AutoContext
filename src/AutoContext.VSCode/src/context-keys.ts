import type { CatalogEntry } from './types/catalog-entry.js';

export class ContextKeys {
    private static readonly settingIdPrefix = 'autocontext.instructions.';
    private static readonly overrideContextPrefix = 'autocontext.override.';

    static overrideKey(settingId: string): string {
        return ContextKeys.overrideContextPrefix + settingId.slice(ContextKeys.settingIdPrefix.length);
    }

    static forEntry(entry: CatalogEntry): readonly string[] {
        return entry.contextKeys ?? [];
    }
}
