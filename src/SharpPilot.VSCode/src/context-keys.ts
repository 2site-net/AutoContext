import type { CatalogEntry } from './catalog-entry.js';

export class ContextKeys {
    private static readonly settingIdPrefix = 'sharppilot.instructions.';
    private static readonly overrideContextPrefix = 'sharppilot.override.';

    static overrideKey(settingId: string): string {
        return ContextKeys.overrideContextPrefix + settingId.slice(ContextKeys.settingIdPrefix.length);
    }

    static forEntry(entry: CatalogEntry): readonly string[] {
        return entry.contextKeys ?? [];
    }
}
