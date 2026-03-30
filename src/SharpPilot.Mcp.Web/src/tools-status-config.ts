import { readFileSync } from 'node:fs';
import { join } from 'node:path';

let configFilePath: string | undefined;

export function configure(workspacePath: string): void {
    configFilePath = join(workspacePath, '.sharppilot.json');
}

export function isEnabled(toolName: string): boolean {
    if (!configFilePath) {
        return true;
    }

    try {
        const json = readFileSync(configFilePath, 'utf-8');
        const doc: unknown = JSON.parse(json);

        if (
            typeof doc === 'object' && doc !== null
            && 'tools' in doc && typeof doc.tools === 'object' && doc.tools !== null
            && 'disabledTools' in doc.tools && Array.isArray(doc.tools.disabledTools)
        ) {
            return !doc.tools.disabledTools.includes(toolName);
        }

        return true;
    } catch {
        return true;
    }
}
