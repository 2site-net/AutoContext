import * as vscode from 'vscode';
import type { InstructionsCatalog } from './instructions-catalog.js';
import type { McpToolsManifest } from './mcp-tools-manifest.js';
import { ContextKeys } from './context-keys.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import type { AutoContextConfigManager } from './autocontext-config.js';
import type { McpToolConfig } from './types/autocontext-config.js';

export class AutoConfigurer {
    constructor(
        private readonly detector: WorkspaceContextDetector,
        private readonly instructionsCatalog: InstructionsCatalog,
        private readonly toolsManifest: McpToolsManifest,
        private readonly configManager: AutoContextConfigManager,
    ) {}

    async run(): Promise<void> {
        const { detector, instructionsCatalog, toolsManifest, configManager } = this;
        const currentConfig = await configManager.read();
        const totalToolItems = toolsManifest.tools.reduce((acc, t) => acc + Math.max(1, t.tasks.length), 0);
        const totalItems = instructionsCatalog.count + totalToolItems;
        let enabled = 0;

        // Apply instruction enable/disable changes individually.
        for (const entry of instructionsCatalog.all) {
            const flags = ContextKeys.forEntry(entry);
            const relevant = flags.length === 0 || flags.some(k => detector.get(k));
            if (relevant) { enabled++; }

            const currentlyEnabled = currentConfig.instructions?.[entry.fileName]?.enabled !== false;
            if (relevant !== currentlyEnabled) {
                await configManager.setInstructionEnabled(entry.fileName, relevant);
            }
        }

        // Build the complete new mcpTools state and write it atomically.
        const currentTools = currentConfig.mcpTools ?? {};
        const newTools: Record<string, McpToolConfig | false> = {};

        for (const tool of toolsManifest.tools) {
            const flags = tool.workspaceFlags;
            const relevant = flags.length === 0 || flags.some(k => detector.get(k));

            if (tool.tasks.length === 0) {
                // Leaf tool
                if (!relevant) {
                    newTools[tool.name] = false;
                } else {
                    const existing = currentTools[tool.name];
                    if (existing !== false && existing !== undefined && Object.keys(existing).length > 0) {
                        // Preserve version if present, just remove enabled: false.
                        const { enabled: _, ...rest } = existing;
                        if (Object.keys(rest).length > 0) {
                            newTools[tool.name] = rest as McpToolConfig;
                        }
                    }
                    enabled++;
                }
                continue;
            }

            for (const task of tool.tasks) {
                if (!relevant) {
                    const existing = newTools[tool.name];
                    if (existing !== false) {
                        const toolEntry: McpToolConfig = (existing as McpToolConfig) ?? {};
                        const arr = toolEntry.disabledTasks ?? [];
                        toolEntry.disabledTasks = [...arr, task.name];
                        newTools[tool.name] = toolEntry;
                    }
                } else {
                    enabled++;
                    // Nothing to add for enabled tasks — absence from disabledTasks means enabled.
                }
            }
        }

        await configManager.setMcpTools(newTools);

        await vscode.window.showInformationMessage(
            `AutoContext: Enabled ${enabled} of ${totalItems} items for this workspace.`,
        );
    }
}
