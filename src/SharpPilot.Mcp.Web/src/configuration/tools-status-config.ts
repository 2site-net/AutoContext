import { readFileSync } from 'node:fs';
import { join } from 'node:path';

export class ToolsStatusConfig {
    private readonly configFilePath: string | undefined;

    constructor(workspacePath?: string) {
        this.configFilePath = workspacePath
            ? join(workspacePath, '.sharppilot.json')
            : undefined;
    }

    isEnabled(toolName: string): boolean {
        if (!this.configFilePath) {
            return true;
        }

        try {
            const json = readFileSync(this.configFilePath, 'utf-8');
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
}
