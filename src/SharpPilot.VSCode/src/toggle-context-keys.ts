import type { CatalogEntry } from './catalog-entry.js';

const settingIdPrefix = 'sharppilot.instructions.';
const overrideContextPrefix = 'sharppilot.override.';

export function overrideContextKey(settingId: string): string {
    return overrideContextPrefix + settingId.slice(settingIdPrefix.length);
}

export function contextKeysForEntry(entry: CatalogEntry): readonly string[] {
    return entry.contextKeys ?? [];
}
