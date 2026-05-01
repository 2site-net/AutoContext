import * as vscode from 'vscode';
import type { AutoContextConfigManager } from './autocontext-config.js';
import type { InstructionsFilesManifest } from './instructions-files-manifest.js';
import type { McpToolsManifest } from './mcp-tools-manifest.js';
import type { AutoContextConfig } from '#types/autocontext-config.js';
import type { Logger } from '#types/logger.js';

export function isToolEnabled(config: AutoContextConfig, toolName: string, taskName?: string): boolean {
    const entry = config.mcpTools?.[toolName];
    if (entry === undefined) return true;
    if (taskName) {
        // Tasks are independent of the parent's enabled state.
        // `entry === false` (shorthand) has no disabledTasks → task is enabled.
        if (entry === false) return true;
        return !entry.disabledTasks?.includes(taskName);
    }
    if (entry === false) return false;
    if (entry.enabled === false) return false;
    return true;
}

/**
 * Wire-shape of the snapshot pushed by `AutoContextConfigServer` to
 * subscribers. Keys are MCP Tool names from the registry; values in
 * `disabledTasks` are MCP Task names belonging to that tool.
 *
 * - `disabledTools` lists every tool whose entry resolves to disabled
 *   (either shorthand `false` or `{ enabled: false }`).
 * - `disabledTasks` lists per-tool task names recorded in
 *   `disabledTasks` on the `.autocontext.json` entry. A tool may
 *   appear in `disabledTasks` regardless of whether it appears in
 *   `disabledTools` — the parent tool's enabled state and its
 *   per-task disable list are independent (mirrors
 *   {@link isToolEnabled}).
 */
export interface DisabledStateSnapshot {
    readonly disabledTools: readonly string[];
    readonly disabledTasks: Readonly<Record<string, readonly string[]>>;
}

/**
 * Projects the canonical `.autocontext.json` config into the
 * tool/task disabled-state slice consumed by `AutoContext.Mcp.Server`
 * (and any future subscriber). Pure function — no IO, no logging.
 */
export function projectDisabledState(config: AutoContextConfig): DisabledStateSnapshot {
    const disabledTools: string[] = [];
    const disabledTasks: Record<string, string[]> = {};

    const tools = config.mcpTools;
    if (tools) {
        for (const [name, entry] of Object.entries(tools)) {
            if (entry === false) {
                disabledTools.push(name);
                continue;
            }
            if (entry.enabled === false) {
                disabledTools.push(name);
            }
            if (entry.disabledTasks && entry.disabledTasks.length > 0) {
                disabledTasks[name] = [...entry.disabledTasks];
            }
        }
    }

    return { disabledTools, disabledTasks };
}

export class ConfigContextProjector implements vscode.Disposable {
    private readonly disposables: vscode.Disposable[] = [];

    constructor(
        private readonly configManager: AutoContextConfigManager,
        private readonly instructionsManifest: InstructionsFilesManifest,
        private readonly toolsManifest: McpToolsManifest,
        private readonly logger: Logger,
    ) {
        this.disposables.push(
            configManager.onDidChange(() =>
                void this.project().catch(err =>
                    this.logger.error('Failed to project config', err),
                ),
            ),
        );
    }

    async project(): Promise<void> {
        const config = await this.configManager.read();
        const setContext = (key: string, value: boolean): Thenable<void> =>
            vscode.commands.executeCommand('setContext', key, value);

        const toolKeys: Thenable<void>[] = [];
        for (const tool of this.toolsManifest.tools) {
            if (tool.tasks.length === 0) {
                toolKeys.push(setContext(tool.runtimeInfo.contextKey, isToolEnabled(config, tool.name)));
            } else {
                for (const task of tool.tasks) {
                    toolKeys.push(setContext(task.runtimeInfo.contextKey, isToolEnabled(config, tool.name, task.name)));
                }
            }
        }

        await Promise.all([
            ...this.instructionsManifest.instructions.map(entry =>
                setContext(entry.runtimeInfo.contextKey, config.instructions?.[entry.name]?.enabled !== false),
            ),
            ...toolKeys,
        ]);
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }
}
