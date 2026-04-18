import * as vscode from 'vscode';
import type { McpToolsCatalog } from './mcp-tools-catalog.js';
import type { AutoContextConfigManager } from './autocontext-config.js';
import type { McpToolConfig } from './types/autocontext-config.js';

export class McpToolsConfigWriter {
    constructor(private readonly configManager: AutoContextConfigManager, private readonly catalog: McpToolsCatalog) {}

    async write(): Promise<void> {
        const config = vscode.workspace.getConfiguration();
        const tools: Record<string, McpToolConfig | false> = {};

        // Group features by parent tool name.
        const parentFeatures = new Map<string, { total: number; disabledFeatures: string[] }>();

        for (const entry of this.catalog.all) {
            const enabled = config.get<boolean>(entry.settingId, true) !== false;

            if (!entry.featureName) {
                // Standalone tool — no parent.
                if (!enabled) {
                    tools[entry.toolName] = false;
                }
                continue;
            }

            // Feature under a parent tool.
            let group = parentFeatures.get(entry.toolName);
            if (!group) {
                group = { total: 0, disabledFeatures: [] };
                parentFeatures.set(entry.toolName, group);
            }
            group.total++;
            if (!enabled) {
                group.disabledFeatures.push(entry.featureName);
            }
        }

        for (const [parentName, group] of parentFeatures) {
            if (group.disabledFeatures.length === 0) {
                continue;
            }
            if (group.disabledFeatures.length === group.total) {
                tools[parentName] = { enabled: false, disabledFeatures: group.disabledFeatures };
            } else {
                tools[parentName] = { disabledFeatures: group.disabledFeatures };
            }
        }

        await this.configManager.setMcpTools(tools);
    }
}
