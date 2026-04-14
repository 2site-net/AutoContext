import type { Checker } from './checker.js';
import { isEditorConfigFilter } from './editorconfig-filter.js';
import type { WorkspaceServerClient } from '../workspace-server/workspace-server-client.js';

/// Base class for aggregate tools that run multiple sub-checkers and return
/// a single combined report. Handles tool-mode resolution via the workspace
/// service, EditorConfig key aggregation, and data merging.
export abstract class CompositeChecker implements Checker {
    abstract readonly toolName: string;

    protected abstract readonly toolLabel: string;

    constructor(
        private readonly workspaceServerClient: WorkspaceServerClient,
    ) {}

    protected abstract createCheckers(): readonly Checker[];

    async check(content: string, data?: Record<string, string>): Promise<string> {
        if (!content.trim()) {
            throw new Error('Source code must not be empty or whitespace.');
        }

        await this.workspaceServerClient.sendLog(
            'Information',
            `Tool invoked: ${this.toolName} | content length: ${content.length}`);

        const checkers = this.createCheckers();

        const { filePath, ...restData } = data ?? {};
        const explicitParams = Object.keys(restData).length > 0 ? restData : undefined;

        const toolNames = checkers.map(c => c.toolName);
        const editorConfigKeys = [...new Set(
            checkers.filter(isEditorConfigFilter).flatMap(c => [...c.editorConfigKeys]),
        )];

        const resolved = await this.workspaceServerClient.resolveTools({
            tools: toolNames,
            filePath,
            'editorconfig-keys': editorConfigKeys.length > 0 ? editorConfigKeys : undefined,
        });

        const sections: string[] = [];

        for (const checker of checkers) {
            const enabled = resolved?.tools[checker.toolName] ?? true;

            if (enabled) {
                await this.workspaceServerClient.sendLog(
                    'Information', `  Running: ${checker.toolName}`);
                const merged = { ...explicitParams, ...resolved?.editorconfig };
                sections.push(await checker.check(content, Object.keys(merged).length > 0 ? merged : undefined));
            } else if (isEditorConfigFilter(checker)) {
                await this.workspaceServerClient.sendLog(
                    'Information', `  Running (editorconfig-only): ${checker.toolName}`);
                const merged = { ...explicitParams, ...resolved?.editorconfig, __disabled: 'true' };
                sections.push(await checker.check(content, merged));
            } else {
                await this.workspaceServerClient.sendLog(
                    'Information', `  Skipped: ${checker.toolName}`);
            }
        }

        if (sections.length === 0) {
            return `⚠️ All ${this.toolLabel} checks are disabled.`;
        }

        const failures = sections.filter(s => s.startsWith('❌'));

        if (failures.length === 0) {
            return `✅ All enabled ${this.toolLabel} checks passed.`;
        }

        return failures.join('\n\n');
    }
}
