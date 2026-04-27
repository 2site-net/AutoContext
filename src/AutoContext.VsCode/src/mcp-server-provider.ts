import * as vscode from 'vscode';
import { join } from 'node:path';
import { existsSync } from 'node:fs';
import type { McpToolsManifest } from './mcp-tools-manifest.js';
import type { HealthMonitorServer } from './health-monitor-server.js';
import type { ServersManifest } from './servers-manifest.js';
import type { WorkerManager } from './worker-manager.js';
import type { AutoContextConfigManager } from './autocontext-config.js';
import type { AutoContextConfig } from '#types/autocontext-config.js';
import { isToolEnabled } from './config-context-projector.js';
import type { Logger } from '#types/logger.js';

const extensionId = '2site-net.autocontext';

/**
 * Label of the single `McpStdioServerDefinition` this provider returns.
 * VS Code exposes the definition to the rest of the system via the id
 * `${extensionId}/${mcpServerDefinitionLabel}`.
 */
const mcpServerDefinitionLabel = 'AutoContext';

/**
 * Registers the single `AutoContext.Mcp.Server` MCP surface with VS Code.
 * Mcp.Server fronts every tool across every worker; per-scope gating and
 * per-tool dispatch live inside Mcp.Server itself, so this provider has
 * no knowledge of scopes, workers, or workspace context.
 */
export class McpServerProvider implements vscode.McpServerDefinitionProvider {
    private readonly mcpServerBinary: string;
    private readonly version: string;
    private _config: AutoContextConfig;
    private readonly disposable: vscode.Disposable;

    readonly onDidChangeMcpServerDefinitions: vscode.Event<void>;

    constructor(
        extensionPath: string,
        version: string,
        onDidChange: vscode.Event<void>,
        private readonly toolsManifest: McpToolsManifest,
        private readonly healthMonitor: HealthMonitorServer,
        private readonly workerManager: WorkerManager,
        serversManifest: ServersManifest,
        configManager: AutoContextConfigManager,
        private readonly logPipeName: string,
        private readonly logger: Logger,
    ) {
        const mcpServerEntry = serversManifest.mcpServer;
        const ext = process.platform === 'win32' ? '.exe' : '';
        this.mcpServerBinary = join(extensionPath, 'servers', mcpServerEntry.name, `${mcpServerEntry.name}${ext}`);
        this.version = version;
        this._config = configManager.readSync();
        this.onDidChangeMcpServerDefinitions = onDidChange;
        this.disposable = configManager.onDidChange(() => {
            void configManager.read().then(c => { this._config = c; }).catch(err =>
                this.logger.error('Failed to update config', err),
            );
        });
    }

    dispose(): void {
        this.disposable.dispose();
    }

    async provideMcpServerDefinitions(): Promise<vscode.McpServerDefinition[]> {
        if (!existsSync(this.mcpServerBinary)) {
            this.logger.warn(`Mcp.Server binary not found at '${this.mcpServerBinary}'; returning no MCP server definitions`);
            return [];
        }
        if (!this.anyToolEnabled()) {
            this.logger.debug('No tools enabled in config; returning no MCP server definitions');
            return [];
        }

        // --log-pipe lets Mcp.Server stream structured logs over the
        // extension's LogServer pipe (its own AutoContext Output channel),
        // matching the workers' wire-up. Mcp.Server falls back to stderr
        // automatically when the switch is absent (e.g. standalone runs).
        const args: string[] = [
            '--endpoint-suffix', this.workerManager.getEndpointSuffix(),
            '--health-monitor', this.healthMonitor.getPipeName(),
            '--log-pipe', this.logPipeName,
        ];

        this.logger.debug(`Returning Mcp.Server definition '${mcpServerDefinitionLabel}' (v${this.version})`);
        return [new vscode.McpStdioServerDefinition(
            mcpServerDefinitionLabel,
            this.mcpServerBinary,
            args,
            undefined,
            this.version,
        )];
    }

    async resolveMcpServerDefinition(server: vscode.McpServerDefinition): Promise<vscode.McpServerDefinition> {
        return server;
    }

    /**
     * Returns the availability status of a (legacy) server label for use in
     * UI indicators. Every legacy label now resolves to the same single
     * Mcp.Server binary; the UI-side collapse happens in a later phase.
     *
     * - `'unavailable'`: the Mcp.Server binary does not exist on disk.
     * - `'disabled'`: the binary exists but every tool is disabled in settings.
     * - `'available'`: the binary exists and at least one tool is enabled.
     */
    getServerStatus(_serverLabel: string): 'unavailable' | 'disabled' | 'available' {
        if (!existsSync(this.mcpServerBinary)) {
            return 'unavailable';
        }
        return this.anyToolEnabled() ? 'available' : 'disabled';
    }

    /**
     * Returns the VS Code internal definition IDs a tree node's command
     * should target. All legacy labels map to the single Mcp.Server
     * definition id until the UI is collapsed.
     */
    getDefinitionIds(_serverLabel: string): string[] {
        return [`${extensionId}/${mcpServerDefinitionLabel}`];
    }

    private anyToolEnabled(): boolean {
        for (const tool of this.toolsManifest.tools) {
            if (tool.tasks.length === 0) {
                if (isToolEnabled(this._config, tool.name)) { return true; }
                continue;
            }
            for (const task of tool.tasks) {
                if (isToolEnabled(this._config, tool.name, task.name)) { return true; }
            }
        }
        return false;
    }
}
