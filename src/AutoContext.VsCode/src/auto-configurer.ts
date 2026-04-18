import * as vscode from 'vscode';
import type { InstructionsCatalog } from './instructions-catalog.js';
import type { McpToolsCatalog } from './mcp-tools-catalog.js';
import { ContextKeys } from './context-keys.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import type { AutoContextConfigManager } from './autocontext-config.js';
import type { McpToolConfig } from './types/autocontext-config.js';
import type { McpToolsCatalogEntry } from './mcp-tools-catalog-entry.js';

export class AutoConfigurer {
    static async configure(detector: WorkspaceContextDetector, instructionsCatalog: InstructionsCatalog, toolsCatalog: McpToolsCatalog, configManager: AutoContextConfigManager): Promise<void> {
        const currentConfig = await configManager.read();
        const allEntries = [...instructionsCatalog.all, ...toolsCatalog.all];
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

        for (const entry of toolsCatalog.all) {
            const flags = ContextKeys.forEntry(entry);
            const relevant = flags.length === 0 || flags.some(k => detector.get(k));
            const catalogEntry = entry as McpToolsCatalogEntry;

            if (!catalogEntry.featureName) {
                // Leaf tool
                if (!relevant) {
                    newTools[catalogEntry.toolName] = false;
                } else {
                    const existing = currentTools[catalogEntry.toolName];
                    if (existing !== false && existing !== undefined && Object.keys(existing).length > 0) {
                        // Preserve version if present, just remove enabled: false.
                        const { enabled: _, ...rest } = existing;
                        if (Object.keys(rest).length > 0) {
                            newTools[catalogEntry.toolName] = rest as McpToolConfig;
                        }
                    }
                    enabled++;
                }
            } else {
                // Feature — accumulate into parent tool's disabledFeatures.
                if (!relevant) {
                    const existing = newTools[catalogEntry.toolName];
                    if (existing !== false) {
                        const toolEntry: McpToolConfig = (existing as McpToolConfig) ?? {};
                        const arr = toolEntry.disabledFeatures ?? [];
                        toolEntry.disabledFeatures = [...arr, catalogEntry.featureName];
                        newTools[catalogEntry.toolName] = toolEntry;
                    }
                } else {
                    enabled++;
                    // Nothing to add for enabled features — absence from disabledFeatures means enabled.
                }
            }
        }

        await configManager.setMcpTools(newTools);

        await vscode.window.showInformationMessage(
            `AutoContext: Enabled ${enabled} of ${allEntries.length} items for this workspace.`,
        );
    }
}
