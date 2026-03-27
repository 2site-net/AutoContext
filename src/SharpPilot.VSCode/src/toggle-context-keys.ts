import type { ToggleEntry } from './toggle-entry.js';

const settingIdPrefix = 'sharppilot.instructions.';
const overrideContextPrefix = 'sharppilot.override.';

export function overrideContextKey(settingId: string): string {
    return overrideContextPrefix + settingId.slice(settingIdPrefix.length);
}

export function contextKeysForEntry(entry: ToggleEntry): readonly string[] {
    return entry.contextKeys ?? [];
}
